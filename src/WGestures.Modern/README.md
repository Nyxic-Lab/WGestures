# WGestures Modern (preview)

This is a **standalone, preliminary WinUI 3 settings shell**, not yet a replacement for the existing WGestures executable. Nothing in this prototype currently modifies gesture configuration or runs input hooks.

## Build

Requirements: Windows 10 2004+ or Windows 11, Visual Studio 2022 or later with Windows App SDK / WinUI development tools, and .NET 10 SDK.

From a Windows developer terminal:

```powershell
dotnet restore src/WGestures.Modern/WGestures.Modern.csproj
dotnet build src/WGestures.Modern/WGestures.Modern.csproj -c Debug -p:Platform=x64
```

This project has not been compiled in the current development environment. Do not assume these instructions establish a passing build; validate on a Windows runner and correct any SDK/XAML compatibility errors before release.

The original solution remains untouched. See [modernization plan](../../docs/modernization.md) for module boundaries, configuration compatibility and integration milestones.

## Current status

- Navigation: Overview, Gestures, Applications, Corners & edges, Preferences, About.
- UI: Fluent NavigationView, responsive scroll region, information and content surfaces.
- Runtime target: .NET 10 LTS and Windows App SDK 2.6.5.
- Missing: live editor, persisted settings, input engine, command registry, import/export, Windows validation, installer.

The original WGestures is licensed under GPL-2.0. Keep upstream notices and review the licensing of all new dependencies.
