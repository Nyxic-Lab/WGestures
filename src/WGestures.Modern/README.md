# Nyxic Gestures 0.1.0 RC4

Nyxic Gestures is a modern Windows mouse gesture manager based on WGestures by YingDev (GPL-2.0). The settings application is code-first C# with .NET 10 WPF: a shared Nyxic design system on both Windows 10 and Windows 11. Windows 11 retains native Fluent window theming; Windows 10 avoids the modern theme path. It has no WinUI or Windows App SDK dependency. A separate .NET 10 process runs the ported original gesture recognizer, mouse and keyboard hooks, and action engine. WinForms/GDI+ remain in the engine for the original tray, clipboard and gesture-overlay integrations.

## Use

1. Install the x64 RC package or extract the portable ZIP. Exit the original WGestures process if it is running.
2. Open Nyxic Gestures. The bundled defaults contain 52 global gestures, 14 app-specific gestures, and eight screen corner/edge actions.
3. Use the visual gesture library to create, draw, edit, delete, order and search gestures, record keyboard shortcuts, select triggers/modifiers, and configure any of the 12 action types. Applications manages executable profiles, enable/disable and inheritance. Corners & edges assigns all eight actions using a screen map. Preferences covers input, feedback, theme, trail colors, pause shortcut and optional autostart.
4. Import an original WG1 version-3 .wg2 configuration, and optionally its config.plist. Save workspace persists the editor draft; **Apply & start / reload** validates and activates it in the separate engine. The tray supports pause/resume/exit; closing settings does not stop the engine.
5. Command-line and Lua actions require explicit opt-in. Hot corners and rub edges are initially disabled for fresh preferences. Arbitrary CLR type metadata is not activated.

## Build and package

The installed x64 runtime targets Windows 10 version 1607 (build 14393) and newer; earlier Windows 10 1507/1511 and x86 are outside the target. Use a currently supported development machine with .NET 10 SDK and Inno Setup 6.3+ for packaging. Visual Studio's WinUI workloads are not required.

```powershell
dotnet run --project tests/WGestures.Modern.Core.Checks/WGestures.Modern.Core.Checks.csproj -c Release
dotnet run --project tests/WGestures.Engine.Checks/WGestures.Engine.Checks.csproj -c Release
dotnet build src/Engine/Nyxic.Gestures.Engine/Nyxic.Gestures.Engine.csproj -c Release -p:Platform=x64
dotnet build src/WGestures.Modern/WGestures.Modern.csproj -c Release -p:Platform=x64
./installer/Build-Release.ps1
```

The release script publishes a self-contained WPF UI and gesture engine, installer, portable ZIP, source and SHA-256 checksums. CI requires an interactive Windows desktop: it validates the installed engine, starts and stops the native hooks without executing actions, launches the installed WPF window, exercises six pages, the theme, twelve command editors and the profile dropdown, captures real WPF render evidence, checks normal launch/close, forces both classic WPF and older per-monitor DPI paths, and verifies uninstall. The hosted Windows runner does not certify an actual 1607 or 1809 installation. A failed test blocks artifact publication.

## Windows compatibility

Both OS paths use shared code-built WPF templates, rounded fields, switches, gesture previews, navigation, and light/dark palettes. Windows 10 skips the native Fluent ThemeMode API; Windows 11 retains Fluent window theming; launch `WGestures.Modern.exe --compatibility` to force classic controls for troubleshooting. The About page and `ui-status.log` report the detected Windows build and selected UI path. Per-monitor DPI v2 is used from build 15063 (1703) onward; earlier builds use first-generation per-monitor DPI.

See [the compatibility matrix and local smoke tests](../../docs/windows-compatibility.md). Windows 10 1607 is a best-effort compatibility target, not a current OS servicing guarantee; install available system updates and use a supported Windows edition for production.

## Status and limitations

RC2 replaced RC1's WinUI settings application after its installed window failed to start in CI. RC3 added version-aware Windows 10 compatibility. RC4 introduces the shared visual system and begins a behavior-tested engine refactor, without rewriting the native input hooks or changing the file format. See [engine audit](../../docs/engine-refactor.md). The engine and configuration contract are unchanged by the UI framework replacement. Microsoft's code-based WPF ThemeMode API is experimental; it is limited to the appearance layer, with a logged fallback. Interactive testing of real gesture actions, multi-monitor/DPI behavior, elevation boundaries, and Lua compatibility is still required before a stable release. Localization, signed binaries and an update service remain future work.

Workspace: %LOCALAPPDATA%\Nyxic-Lab\Gestures. Startup errors are logged to ui-status.log. The legacy WGestures files are not overwritten. Failed activation attempts to restore the previous active files; it does not silently restart an old engine.
