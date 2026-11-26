# Nyxic Gestures modernization

Working product name: **Nyxic Gestures**. The original `master` branch remains the upstream reference until the new application is validated.

## Target architecture

- **Runtime:** .NET 10 LTS.
- **Desktop UI:** Windows App SDK 2.7.0 / WinUI 3, Fluent controls, light/dark/system theme.
- **Configuration:** standalone `WGestures.Modern.Core` library, System.Text.Json, original version-3 documents with opaque command payloads and a separate draft path. No required account, remote service, or telemetry.
- **Input:** port native hooks, gesture recognition and command execution behind explicit interfaces. Evaluate a lightweight separate engine process and a local IPC contract before adding complexity.
- **Distribution:** unpackaged desktop build first, then signed installers or MSIX as appropriate. Do not reuse upstream signing certificates or update endpoints.

## Legacy boundaries

The legacy settings UI uses a ~115 KB WinForms designer and ~49 KB code-behind with data binding and event handlers. The original projects use .NET Framework 4.8, legacy NLua/KeraLua/Newtonsoft.Json and upstream post-build certificate signing. Keep this baseline unchanged while migrating.

## Implementation progress

- [x] Isolated modern settings shell with six navigation sections.
- [x] Portable version-3 gesture-document reader, preserving raw JSON and never resolving imported CLR type metadata.
- [x] Actual overview metrics, profile filtering, search, row inspection, gesture name/path edits, isolated draft storage and appearance selector.
- [x] Core regression check program based on upstream defaults; Windows CI workflow added (execution status must be checked separately).
- [ ] Validate the modern application on Windows and fix any build / launch issues.
- [ ] Complete the settings editor: trigger/modifier, commands, new/delete/reorder, per-app settings, corner editor, localization, migration of config.plist.
- [ ] Port the engine and Windows hook implementation, test DPI, multiple displays, input state and command semantics.
- [ ] Wire the editor to the engine, verify migration and upgrade behavior, and replace upstream branding/update/install integration.
- [ ] Produce an independently signed release after testing.

The modern preview does not control the legacy gesture process. Do not describe it as a functional mouse gesture utility until integration is complete. Keep GPL-2.0 and upstream attribution intact.
