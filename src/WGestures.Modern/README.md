# Nyxic Gestures — modern workspace preview

This project is a separate native WinUI 3 application targeting .NET 10. It loads original WGestures version-3 gesture documents as data, not as executable CLR types. It **does not yet capture mouse gestures or execute commands**.

## Build and check on Windows

Requirements: Windows 10 2004+ / Windows 11, .NET 10 SDK, Visual Studio/MSBuild with Windows application development tooling, and the Windows App SDK 2.7 runtime for the framework-dependent build.

```powershell
dotnet run --project tests/WGestures.Modern.Core.Checks/WGestures.Modern.Core.Checks.csproj -c Release
msbuild src/WGestures.Modern/WGestures.Modern.csproj /restore /p:Configuration=Release /p:Platform=x64
```

The CI workflow performs the configuration checks and an x64 Windows build. A passing build does not establish runtime/UI parity; interactive Windows smoke testing remains necessary.

## Using the preview

- The Overview page summarizes the bundled or loaded document. The original bundled defaults contain 66 gestures, including 52 global gestures, and three application-specific profiles.
- Gestures: select a profile, search by name/direction/command, choose a row, edit its name or direction, apply the edit, then explicitly save the draft. The trigger, modifier, ordering, command and unknown metadata are preserved.
- Applications: inspect profile state, executable path and inherited global configuration.
- Corners & edges: inspect the eight slots in their persisted order.
- Preferences: switch System, Light and Dark themes for the current session.
- Import: load an original WG1 version-3 .wg2 document. No arbitrary .NET types are activated.

The preview reads the original bundled defaults if no draft exists. Save draft writes only to `%LOCALAPPDATA%\Nyxic-Lab\Gestures\workspace.wg2`. The original selected file is not overwritten. Do not load an untrusted draft into the legacy engine: its original serializer may resolve CLR types.

## Limitations

The product name is provisional; the repository, license, upstream notices and legacy solution remain intact. No installer, background gesture service, real-time legacy sync, command/trigger/ modifier editor, localization or release build is provided yet. Preferences are session-only. Name/path changes must be applied and saved explicitly.

See [modernization plan](../../docs/modernization.md).
