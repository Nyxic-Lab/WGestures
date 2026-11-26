# Gesture engine refactor — RC4

## Objective

Keep the gesture feel, compatibility workarounds, legacy script surface and v3 configuration contract unless an intentional behavior change is backed by a reproducible regression. Refactor the engine in small layers instead of replacing Win32 hooks, geometry, rendering and command execution simultaneously.

Baseline: RC3 `c2639c6dd31ce1231a6d0f0e305ee8ed4daa91f9` passed hosted configuration, install, hook and both UI startup paths. It was not a long-running native input stress test.

## Stage 1 — implemented

| Module | Finding | Change and evidence |
|---|---|---|
| `GestureParser.Parse` | The first stroke's Y delta subtracted StartPoint.X, making diagonal correction depend on absolute screen coordinates. | Use StartPoint.Y; replay the same path at origin, translated coordinates, and negative monitor coordinates. |
| `GestureIntent.Execute` | Release builds swallowed propagated command exceptions and returned successful ExecutionResult; command preparation was outside the catch. | Return the actual exception and failure result, protect preparation/activation/execution, and log failures in the engine. Commands that catch their own failures remain a separate audit. |
| `GestureParser` | Modifier callbacks could fail or be interrupted without reliably detaching ReportStatus or ending the session. Duplicate path-end notifications could re-execute. | Explicit path-active and modifier-session state; prepare command context before callbacks; release session on end/timeout/dispose; stop a failed session from restarting on the same path. |
| `GestureModifier.All` | X1/X2 were omitted. | Include both bits; test observed versus filtered modifier sets. |
| `CmdCommand` | WG_ENDPOINT_Y incorrectly copied endpoint X. | Use endpoint Y. |
| Per-action memory management | Normal execution trimmed the process working set, and selected commands forced full GC. | Remove working-set trim and forced GC from the reviewed command paths; no unmeasured performance claim. |
| `Win32MousePathTracker2.Start` | Queue consumer used a single if before Monitor.Wait. | Recheck the empty predicate with while. This is not the transport redesign. |
| Auxiliary actions | Hot corner/edge command exceptions could escape the parser. | Contain/report propagated failures through the common execution boundary, without inventing a window target. |

Tests: `dotnet run --project tests/WGestures.Engine.Checks/WGestures.Engine.Checks.csproj -c Release` (Windows). The test project drives the real GestureParser with FakeTracker, FakeContext and FakeFinder. It verifies geometry, execution outcomes, release/timeout/disposal, duplicate end, modifier failure and side-button filtering. It does not install a hook, send OS input, prove scheduling behavior or measure latency. CI runs these checks in both workflows. Intermediate stage passed 18 behavior checks plus 54 existing configuration checks.

## Stage 2 — input transport and capture ownership

Audit source: `WGestures.Core/Impl/Windows/Win32MousePathTracker2.cs`, `MouseHook.cs`.

- Queue messages currently carry only a message type and integer parameter; the hook updates _curPos and _gestureBtn, while the worker reads those shared fields at dequeue and inside event handlers. A backlog can therefore pair an earlier move/release with later pointer coordinates or another trigger.
- The preflight hook and worker both call UpdateContextAndEventArgs on reused _currentContext/_currentEventArgs. In the foreground-target branch ProcId is assigned but WinId is not refreshed, so target identity can be inconsistent.
- Timers and hook/worker state share fields including _moveCount, _captured, _isTimeout and modifier timestamps. Pause and stop are requests, not a fully serialized state machine.
- X-button admission checks the combined X mask, and X-button release currently compares only WM_XBUTTONUP, not which side button was released. Both admission and release need button-identity tests.
- Hook installation should ensure a message queue exists before acknowledging readiness, and shutdown should join even on partial install/failure. _uinstall uses a short-circuit conjunction, risking the second unhook not being attempted if the first fails.

Proposed migration: introduce immutable InputEvent records (kind, timestamp, position, trigger/button identity, flags, session ID, target snapshot). Separate hook-side synchronous suppression decisions from worker-owned recognition state. Preserve FIFO and never coalesce down/up/modifier boundaries. Coalesce move events only after measured path-loss analysis. Serialize pause/stop/cancel/timeout with the worker. Add a fake native-hook adapter and deterministic trace replay before changing real suppression/replay behavior. Include backlog, rapid consecutive gestures, X1/X2 independently, delayed right-click and drag replay, injected events, left-handed configuration, pause mid-path, fullscreen, and multi-monitor cases. Avoid blocking the low-level hook with command execution or UI.

## Stage 3 — recognition and command contracts

- The drawing pad uses GestureStroke with eight evenly spaced sectors; GestureParser has asymmetric first-stroke diagonal rules and four-direction later segments. Editor paths may not match runtime recognition. The UI allows 32 arrows while the legacy parser defaults to 12. Treat this as a product/compatibility question: preserve legacy mode, create a pure recognizer that both capture and runtime can share, replay old gesture traces, compare exact output and false positives, then make a documented decision on path limits and diagonal support.
- The existing mutable Gesture key includes a mutable direction list. A modern engine should build an immutable GestureKey when indexing configuration, not mutate keys in a dictionary.
- WindowControlCommand uses the window at the start coordinate. Auxiliary actions do not have a reliable target-context contract. Define foreground-window versus pointer-window semantics explicitly; do not use stale context from a previous gesture.
- Cmd, Lua, window activation, clipboard and key simulation need separate cancellation/ownership/error policies. Long commands can block recognition. Some commands catch internal failures or launch a separate thread, so a successful synchronous ExecutionResult does not yet prove their external effects. Preserve Alt/Tab release and input reset on exceptions. Introduce typed ActionResult outcomes and test safe fake commands before any executor concurrency changes.
- Audit the Lua CLR compatibility surface and ensure script actions remain opt-in. Benchmark repeated gestures, p50/p95/p99 latency, queue depth, CPU, working set and handle counts under controlled synthetic/native traces. Measure rather than assume improvements.

## Review boundary

No native Hook transport rewrite, replacement recognizer, execution scheduler, old Windows VM validation or latency benchmark is claimed in RC4. Keep the upstream-compatible Win32 paths and the verified RC3 package available while reviewing the staged changes. Manual Windows 10/11 gesture and real command tests remain necessary before a stable release.
