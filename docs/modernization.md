# WGestures modernization

This fork is being modernized in a separate branch. The original `master` branch remains the reference baseline.

## Target architecture

- **Runtime:** .NET 10 LTS (`net10.0-windows`).
- **Desktop UI:** Windows App SDK / WinUI 3 with Fluent controls.
- **Core:** C# class libraries decoupled from UI concerns, with the original gesture engine ported and characterized through tests before replacement.
- **Input:** Windows native hooks and input injection behind explicit interfaces; do not put input hooks in the UI process long-term.
- **Storage:** local, versioned configuration with migration from the existing format; no required account, remote service, or telemetry.
- **Distribution:** unpackaged desktop build first, then signed installers or MSIX as appropriate; do not reuse upstream signing certificates or update endpoints.

## Current legacy boundaries

`WGestures.App/Gui/Windows/SettingsForm.Designer.cs` (~115 KB) and `SettingsForm.cs` (~49 KB) are tightly coupled WinForms UI/controller code. The app and the supporting projects currently target .NET Framework; the app references legacy NLua, KeraLua and Newtonsoft.Json versions and invokes an upstream certificate-signing command as a post-build step.

## Migration milestones

1. Introduce a compile-independent WinUI 3 settings shell in `src/WGestures.Modern` with navigation and an accessible light/dark theme. Keep the legacy build untouched.
2. Extract reusable settings and gesture contracts into a modern .NET 10 class library. Add serialization tests and a configuration migration strategy.
3. Port the Windows hook, gesture recognition and command execution subsystems; test pointer input, DPI, multi-display and error recovery.
4. Wire the WinUI editor to the ported implementation and add end-to-end Windows smoke tests.
5. Replace upstream branding/update/install integration and build a new release pipeline once modern runtime behavior is verified.

Do not ship the new UI as if it controls gestures until its backend is connected. Changes to trigger semantics and action execution should have regression tests.
