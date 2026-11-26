# Nyxic Gestures 0.1.0 RC

Nyxic Gestures is a modern Windows mouse gesture manager based on WGestures by YingDev (GPL-2.0). The settings UI uses WinUI 3 / .NET 10; the background gesture engine uses ported original recognition, mouse and keyboard hooks, and command execution modules on .NET 10. WinForms/GDI+ remain in the engine only for the legacy tray, clipboard, and gesture rendering integrations.

## Use

1. Install the x64 RC package, or extract the portable ZIP. Exit the original WGestures if it is running.
2. Open Nyxic Gestures. The bundled defaults include 52 global gestures, 14 app-specific gestures, and eight screen corner/edge commands.
3. Use Gestures to draw or edit a gesture, record a keyboard shortcut, select triggers/modifiers, configure one of the 12 supported action types, add/remove/reorder actions; Applications for executable profiles, enable/disable and inheritance; Corners & edges to set all eight actions; Preferences for input/feedback, theme, colors, pause shortcut, and optional autostart.
4. Import an original WG1 version-3 .wg2 configuration and optionally its config.plist. Save workspace persists the editor draft; **Apply & start / reload** validates and activates it in the separate engine. The tray supports pause/resume/exit; closing settings does not stop the engine.
5. Command-line and Lua actions are disabled until explicitly enabled. Hot corners and rub edges are initially disabled in fresh preferences. Imported arbitrary CLR types are rejected at activation; the reader never activates a type from imported metadata.

## Build and package

Requires Windows 10 2004+ or Windows 11, .NET 10 SDK, Visual Studio/MSBuild with WinUI tooling, and Inno Setup 6.3+ for packaging. From a developer terminal:

```powershell
dotnet run --project tests/WGestures.Modern.Core.Checks/WGestures.Modern.Core.Checks.csproj -c Release
dotnet build src/Engine/Nyxic.Gestures.Engine/Nyxic.Gestures.Engine.csproj -c Release -p:Platform=x64
msbuild src/WGestures.Modern/WGestures.Modern.csproj /restore /p:Configuration=Release /p:Platform=x64
./installer/Build-Release.ps1
```

The release script generates self-contained UI + engine installer and portable package, corresponding source and SHA-256 checksums in artifacts/release. CI checks compile, configuration tests, installed engine validation, installer/uninstaller, and native hook/window smoke tests when an interactive desktop is available.

## Status and limits

This is a functional release candidate, not a stable production release. The original master is unchanged. Manual verification of actual mouse input, action execution, multi-monitor/DPI, elevation boundaries and Lua compatibility is required before stable release. The new settings UI is English; localization and a new update system are not yet implemented. Some old integration behaviors are retained temporarily through WinForms and GDI+ in the engine. Both executables are unsigned.

Data directory: %LOCALAPPDATA%\Nyxic-Lab\Gestures. The legacy WGestures files are not overwritten. A failed activation attempts to restore the previous active configuration; it does not silently restart the old engine.
