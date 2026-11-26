# Nyxic Gestures modernization

## Architecture

- Settings application: .NET 10 / WinUI 3, Windows App SDK 2.7.0, Fluent UI. The settings process can exit while the gesture engine continues.
- Portable configuration: System.Text.Json, version-3 legacy documents, explicit command catalog and field allowlist, independent workspace and active snapshots. Original CLR type metadata is not resolved.
- Background engine: .NET 10 Windows SDK-style projects linking original WindowsInput, WGestures.Common, WGestures.Core and WGestures.View code. Win32 hooks, recognizer and gesture overlay are retained. Only UI and integration boundaries are migrated initially; WinForms/GDI+ are retained where the original engine depends on them.
- Commands: allowlisted typed command creation, NLua 1.7.9 / KeraLua 1.4.9, Newtonsoft.Json 13.0.4 temporarily for original command serialization attributes (not for activation). Remove SHDocVw compile dependency and old URL/IE behavior from modern command ports.
- Distribution: separate x64 self-contained engine and UI packaged with Inno Setup; start/stop validation and safe upgrade/uninstall. Old installer/signing infrastructure is not used.

## Implementation

- [x] Real profile and gesture CRUD, ordering, trigger/modifier, drawing pad, shortcut recording, all 12 command models, eight corner/edge assignments.
- [x] Preferences, theme, colors, pause key, optional autostart; original .wg2 and config.plist import; config export.
- [x] Port engine assemblies, explicit allowlisted command creation and tray lifecycle.
- [x] Apply/reload/stop controls; validated snapshot, rollback of prior active files on failure.
- [x] Expanded configuration regressions, engine build/validation CI, release candidate packaging and install/uninstall/native startup smoke tests.
- [ ] Confirm newest CI and interactive Windows testing; characterize actual action execution, input reliability, script compatibility, DPI/elevation/multi-monitor behavior.
- [ ] Implement localization, signed releases, own update mechanism; consider reducing Windows App SDK dependency footprint and replacing remaining WinForms/GDI+ integration code.
- [ ] Merge only after review and validation.

Original `master` remains the reference baseline. Keep GPL-2.0 and upstream attribution intact.
