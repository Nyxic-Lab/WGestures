# Nyxic Gestures modernization

## Architecture

- Settings application: .NET 10 WPF, code-first C#, official Fluent theme and custom layout. No WinUI/Windows App SDK or third-party UI packages. The settings process can exit while the gesture engine continues.
- Portable configuration: System.Text.Json, version-3 legacy documents, explicit command catalog and field allowlist, independent workspace and active snapshots. Original CLR type metadata is not resolved.
- Background engine: .NET 10 Windows SDK-style projects linking original WindowsInput, WGestures.Common, WGestures.Core and WGestures.View code. Win32 hooks, recognizer and gesture overlay are retained. WinForms/GDI+ are retained where the original engine depends on them.
- Commands: allowlisted typed command creation, NLua 1.7.9 / KeraLua 1.4.9, Newtonsoft.Json 13.0.4 temporarily for original command serialization attributes (not for activation). Removed SHDocVw compile dependency and modernized URL/IE behavior.
- Distribution: self-contained x64 engine and UI packaged with Inno Setup; start/stop validation and safe upgrade/uninstall. No old installer/signing infrastructure.

## RC2 decision

RC1's engine build and 46 configuration checks passed, and its installed engine passed hook startup/shutdown. Its WinUI window failed to start (0xC000027B). RC2 replaces that UI with WPF rather than suppressing launch verification. Original configuration format, executable entry names and engine remain compatible with the RC1 design. Gesture and action edits use dedicated dialogs. WPF smoke validation opens a real window and exercises all six pages, command editors and theme modes without activating gestures or overwriting user data.

## Implementation

- [x] Real profile and gesture CRUD, ordering, trigger/modifier, drawing pad, shortcut recording, all 12 command models, eight corner/edge assignments.
- [x] Preferences, theme, colors, pause key, optional autostart; original .wg2 and config.plist import; config export.
- [x] Port engine assemblies, explicit allowlisted command creation and tray lifecycle.
- [x] Apply/reload/stop controls; validated snapshot, rollback of prior active files on failure.
- [x] Replace WinUI and Windows App SDK with WPF. Add UI startup diagnostics and expanded installed UI smoke tests.
- [ ] Confirm RC2 CI and interactive Windows testing; characterize actual action execution, input reliability, script compatibility, DPI/elevation/multi-monitor behavior.
- [ ] Implement localization, signed releases, own update mechanism; consider replacing remaining WinForms/GDI+ engine integration code.
- [ ] Merge only after review and validation.

Original `master` remains the reference baseline. Keep GPL-2.0 and upstream attribution intact.
