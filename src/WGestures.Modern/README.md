# Nyxic Gestures 0.1.0 RC2

Nyxic Gestures is a modern Windows mouse gesture manager based on WGestures by YingDev (GPL-2.0). The settings application is code-first C# with .NET 10 WPF and Microsoft's Fluent theme. It has no WinUI or Windows App SDK dependency. A separate .NET 10 process runs the ported original gesture recognizer, mouse and keyboard hooks, and action engine. WinForms/GDI+ remain in the engine for the original tray, clipboard and gesture-overlay integrations.

## Use

1. Install the x64 RC package or extract the portable ZIP. Exit the original WGestures process if it is running.
2. Open Nyxic Gestures. The bundled defaults contain 52 global gestures, 14 app-specific gestures, and eight screen corner/edge actions.
3. Use Gestures to create, draw, edit, delete, order and search gestures, record keyboard shortcuts, select triggers/modifiers, and configure any of the 12 action types. Applications manages executable profiles, enable/disable and inheritance. Corners & edges assigns all eight actions. Preferences covers input, feedback, theme, trail colors, pause shortcut and optional autostart.
4. Import an original WG1 version-3 .wg2 configuration, and optionally its config.plist. Save workspace persists the editor draft; **Apply & start / reload** validates and activates it in the separate engine. The tray supports pause/resume/exit; closing settings does not stop the engine.
5. Command-line and Lua actions require explicit opt-in. Hot corners and rub edges are initially disabled for fresh preferences. Arbitrary CLR type metadata is not activated.

## Build and package

Requires Windows 10 2004+ or Windows 11, .NET 10 SDK, and Inno Setup 6.3+ for packaging. Visual Studio's WinUI workloads are not required.

```powershell
dotnet run --project tests/WGestures.Modern.Core.Checks/WGestures.Modern.Core.Checks.csproj -c Release
dotnet build src/Engine/Nyxic.Gestures.Engine/Nyxic.Gestures.Engine.csproj -c Release -p:Platform=x64
dotnet build src/WGestures.Modern/WGestures.Modern.csproj -c Release -p:Platform=x64
./installer/Build-Release.ps1
```

The release script publishes a self-contained WPF UI and gesture engine, installer, portable ZIP, source and SHA-256 checksums. CI requires an interactive Windows desktop: it validates the installed engine, starts and stops the native hooks without executing actions, launches the installed WPF window, exercises six pages, the theme and twelve command editors, checks normal launch/close, and verifies uninstall. A failed test blocks artifact publication.

## Status and limitations

RC2 replaces RC1's WinUI settings application after its installed window failed to start in CI. The engine and configuration contract are unchanged by the UI framework replacement. Microsoft's code-based WPF ThemeMode API is experimental; it is limited to the appearance layer, with a logged fallback. Interactive testing of real gesture actions, multi-monitor/DPI behavior, elevation boundaries, and Lua compatibility is still required before a stable release. Localization, signed binaries and an update service remain future work.

Workspace: %LOCALAPPDATA%\Nyxic-Lab\Gestures. Startup errors are logged to ui-status.log. The legacy WGestures files are not overwritten. Failed activation attempts to restore the previous active files; it does not silently restart an old engine.
