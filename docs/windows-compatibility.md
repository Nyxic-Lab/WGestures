# Windows compatibility — Nyxic Gestures RC3

The unified x64 package keeps the same .NET 10 engine, gesture format, installer identity and settings storage. There is no separate legacy fork and no Windows App SDK dependency.

| Windows version | Build | UI policy | DPI policy | Status |
|---|---:|---|---|---|
| Windows 10 1507 / 1511 | 10240 / 10586 | Not targeted | Not targeted | Below declared runtime minimum |
| Windows 10 1607 / LTSB 2016 | 14393 | Classic WPF | PerMonitor | Best-effort compatibility target; OS servicing ended |
| Windows 10 1703–1803 | 15063–17134 | Classic WPF | PerMonitorV2 | Best-effort compatibility target |
| Windows 10 1809 / LTSC 2019 | 17763 | Classic WPF | PerMonitorV2 | Compatibility target |
| Windows 10 1903–22H2 / LTSC 2021 | 18362–19045 | Classic WPF | PerMonitorV2 | Compatibility target |
| Windows 11 | 22000+ | Fluent, classic fallback | PerMonitorV2 | Modern target |

Only x64 packages are published. Consult Microsoft's [.NET Windows support table](https://learn.microsoft.com/en-us/dotnet/core/install/windows) and [Windows release health/lifecycle](https://learn.microsoft.com/en-us/windows/release-health/supported-versions-windows-client) for current vendor support and servicing requirements. Allowing installation on an old build does not make an end-of-life OS secure or supported.

## Test on an actual legacy machine

Use a fully patched x64 Windows 10 1607/1809 VM with an interactive desktop and no running original WGestures or Nyxic engine. Test the installer and portable payload. From PowerShell in the installed or extracted directory:

```powershell
$log = Join-Path $env:TEMP 'nyxic-wpf-legacy.log'
$p = Start-Process .\WGestures.Modern.exe -ArgumentList @('--smoke-ui', ('"' + $log + '"')) -Wait -PassThru
Get-Content $log
$p.ExitCode
$p = Start-Process .\engine\Nyxic.Gestures.Engine.exe -ArgumentList '--smoke-hooks' -Wait -PassThru
$p.ExitCode
```

Both exit codes must be zero. The UI log should identify the real Windows build and classic compatibility mode. The smoke-hook test uses an empty disabled configuration, and does not execute gesture actions. Check engine-status.log under %LOCALAPPDATA%\Nyxic-Lab\Gestures for the chosen DPI mode.

Then manually check: normal UI startup, six settings pages, application profiles, drawing and editing gestures, saving and applying configuration, actual mouse gesture recognition, a harmless keyboard action, tray pause/resume/exit, resolution and mixed-DPI displays, upgrade and uninstall. Preserve logs and Windows build number when reporting issues. Windows 10 1607 is not yet verified on real hardware/VM in this PR.

## Hosted CI

The release script requires a real interactive Windows runner. It runs both native hook startup/shutdown paths and both WPF startup paths (normal and forced compatibility), along with the configuration tests, six pages, twelve action editors, theme modes, install/uninstall and package policy checks. These tests exercise the compatibility code on a modern OS; they do not emulate or certify Windows 10 1607.

For troubleshooting the UI on Windows 11, launch `WGestures.Modern.exe --compatibility`. This selects classic controls for that process; it does not modify the OS or stored gesture configuration.
