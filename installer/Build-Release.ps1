[CmdletBinding()]
param(
    [string]$Version = '0.1.0-rc.2',
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^\d+\.\d+\.\d+(-(preview|rc)\.\d+)?$') {
    throw "Unsupported release version: $Version"
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts\release' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$stage = Join-Path $output ('stage-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null

function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}

Push-Location $repo
try {
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not resolve source commit.' }
    $project = Join-Path $repo 'src\WGestures.Modern\WGestures.Modern.csproj'
    Invoke-Checked 'dotnet' @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        '-p:Platform=x64', '-p:PublishSingleFile=false', '-o', $payload)

    $engineDir = Join-Path $payload 'engine'
    $engineProject = Join-Path $repo 'src\Engine\Nyxic.Gestures.Engine\Nyxic.Gestures.Engine.csproj'
    Invoke-Checked 'dotnet' @('publish', $engineProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        '-p:Platform=x64', '-o', $engineDir)

    foreach ($file in @('engine\Nyxic.Gestures.Engine.exe', 'engine\WGestures.Core.dll',
        'engine\WGestures.View.dll', 'engine\NLua.dll', 'engine\KeraLua.dll',
        'WGestures.Modern.exe', 'WGestures.Modern.dll', 'coreclr.dll',
        'PresentationFramework.dll', 'PresentationFramework.Fluent.dll', 'defaults\gestures.wg2')) {
        if (-not (Test-Path (Join-Path $payload $file) -PathType Leaf)) {
            throw "Publish output missing required file: $file"
        }
    }

    if (Test-Path (Join-Path $payload 'Microsoft.UI.Xaml.dll')) {
        throw 'Obsolete WinUI runtime unexpectedly present in the WPF package.'
    }
    $deps = Get-Content (Join-Path $payload 'WGestures.Modern.deps.json') -Raw
    if ($deps -match 'Microsoft\.WindowsAppSDK|Microsoft\.WinUI') {
        throw 'Unexpected Windows App SDK dependency in settings application.'
    }
    Write-Host 'PASS WPF payload contains no WinUI or Windows App SDK dependency'

    Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $payload 'LICENSE.txt')
    @"
Nyxic Gestures $Version (Windows x64)
A community continuation of WGestures by YingDev, under GPL-2.0.

This release candidate contains the native WPF settings interface and
a port of the original gesture recognition and command execution engine.
Open the UI, edit settings, then select Apply & start. Exit the original
WGestures process first. Command-line and Lua actions require explicit opt-in.
Your workspace is stored in %LOCALAPPDATA%\Nyxic-Lab\Gestures.
Uninstalling does not delete the workspace or the legacy installation.
The settings application uses .NET 10 WPF with the Fluent theme.
Interactive Windows testing of actual gesture actions is still required.

This build is unsigned. Only install builds from sources you trust.

Source commit: $commit
Corresponding source: https://github.com/Nyxic-Lab/WGestures/tree/$commit
"@ | Set-Content (Join-Path $payload 'RELEASE-NOTES.txt') -Encoding utf8

    $fixture = Join-Path $payload 'defaults\gestures.wg2'
    $validationSettings = Join-Path $stage 'validation.json'
    '{"AllowScripts":true}' | Set-Content $validationSettings -Encoding utf8
    $engineExe = Join-Path $engineDir 'Nyxic.Gestures.Engine.exe'
    $validation = Start-Process $engineExe -ArgumentList @('--validate', ('"' + $fixture + '"'), ('"' + $validationSettings + '"')) -Wait -PassThru
    if ($validation.ExitCode -ne 0) { throw 'Published engine could not validate bundled actions.' }

    # Use the Windows runner's Inno Setup installation, or the local PATH.
    $iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($iscc) { $compiler = $iscc.Source }
    else {
        $compiler = Join-Path '\Program Files (x86)\Inno Setup 6' 'ISCC.exe'
        $compiler = "$env:SystemDrive$compiler"
        if (-not (Test-Path $compiler)) {
            $compiler = Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'
        }
        if (-not (Test-Path $compiler)) {
            throw 'Inno Setup 6.3+ is required. Install it before running this script.'
        }
    }

    $setup = Join-Path $output "NyxicGestures-$Version-win-x64-Setup.exe"
    $source = Join-Path $output "NyxicGestures-$Version-source.zip"
    $portable = Join-Path $output "NyxicGestures-$Version-win-x64-portable.zip"
    $checksum = Join-Path $output 'SHA256SUMS.txt'
    foreach ($file in @($setup, $source, $portable, $checksum)) {
        if (Test-Path $file) { Remove-Item $file -Force }
    }

    Invoke-Checked $compiler @("/DPayloadDir=$payload", "/DOutputDir=$output",
        "/DAppVersion=$Version", (Join-Path $PSScriptRoot 'NyxicGestures.iss'))
    if (-not (Test-Path $setup -PathType Leaf)) { throw 'Installer was not created.' }

    [IO.Compression.ZipFile]::CreateFromDirectory($payload, $portable)
    Invoke-Checked 'git' @('archive', '--format=zip', "--output=$source", $commit)

    # Exercise the real installed WPF UI, native hooks, and install/uninstall on CI.
    if ($env:CI -eq 'true') {
        $testDir = Join-Path $env:RUNNER_TEMP ('NyxicGesturesSmoke-' + [Guid]::NewGuid().ToString('N'))
        try {
            $process = Start-Process -FilePath $setup -ArgumentList @(
                '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-',
                "/DIR=""$testDir""", '/TASKS=""') -Wait -PassThru
            if ($process.ExitCode -ne 0) { throw "Installer smoke test failed: $($process.ExitCode)" }
            foreach ($file in @('WGestures.Modern.exe', 'coreclr.dll', 'PresentationFramework.dll', 'PresentationFramework.Fluent.dll',
                'engine\Nyxic.Gestures.Engine.exe', 'engine\WGestures.Core.dll', 'defaults\gestures.wg2', 'LICENSE.txt')) {
                if (-not (Test-Path (Join-Path $testDir $file))) {
                    throw "Installed payload missing: $file"
                }
            }
            $installedEngine = Join-Path $testDir 'engine\Nyxic.Gestures.Engine.exe'
            $installedFixture = Join-Path $testDir 'defaults\gestures.wg2'
            $validation = Start-Process $installedEngine -ArgumentList @('--validate', ('"' + $installedFixture + '"'), ('"' + $validationSettings + '"')) -Wait -PassThru
            if ($validation.ExitCode -ne 0) { throw 'Installed engine validation failed.' }
            Write-Host 'PASS installed engine validates real configuration'

            if ([Environment]::UserInteractive) {
                $hookTest = Start-Process $installedEngine -ArgumentList '--smoke-hooks' -PassThru
                if (-not $hookTest.WaitForExit(20000)) {
                    Stop-Process -Id $hookTest.Id -Force
                    throw 'Native hook lifecycle smoke test timed out.'
                }
                if ($hookTest.ExitCode -ne 0) {
                    $log = Join-Path $env:LOCALAPPDATA 'Nyxic-Lab\Gestures\engine-status.log'
                    if (Test-Path $log) { Get-Content $log }
                    throw "Native hook lifecycle smoke test failed: $($hookTest.ExitCode)"
                }
                Write-Host 'PASS native hook startup and shutdown (no active actions)'
                $uiSmokeLog = Join-Path $stage 'wpf-smoke.log'
                $uiExe = Join-Path $testDir 'WGestures.Modern.exe'
                $smoke = Start-Process $uiExe -ArgumentList @('--smoke-ui', ('"' + $uiSmokeLog + '"')) -WorkingDirectory $testDir -PassThru
                try {
                    if (-not $smoke.WaitForExit(45000)) {
                        Stop-Process -Id $smoke.Id -Force
                        throw 'WPF smoke test timed out.'
                    }
                    if (Test-Path $uiSmokeLog) { Get-Content $uiSmokeLog }
                    if ($smoke.ExitCode -ne 0) { throw "WPF smoke test failed: $($smoke.ExitCode)" }
                    if (-not (Test-Path $uiSmokeLog) -or -not (Select-String -Path $uiSmokeLog -Pattern 'PASS WPF window, six pages, theme and twelve action editors' -Quiet)) {
                        throw 'WPF smoke test did not reach the full UI pass marker.'
                    }
                    Write-Host 'PASS installed WPF UI, six pages, theme and twelve command editors'
                }
                finally {
                    if (-not $smoke.HasExited) { Stop-Process -Id $smoke.Id -Force }
                }

                $ui = Start-Process $uiExe -WorkingDirectory $testDir -PassThru
                try {
                    $deadline = [DateTime]::UtcNow.AddSeconds(20)
                    do {
                        Start-Sleep -Milliseconds 250
                        $ui.Refresh()
                        if ($ui.HasExited) {
                            $uiLog = Join-Path $env:LOCALAPPDATA 'Nyxic-Lab\Gestures\ui-status.log'
                            if (Test-Path $uiLog) { Get-Content $uiLog }
                            throw "Normal WPF launch failed: $($ui.ExitCode)"
                        }
                    } while ($ui.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
                    if ($ui.MainWindowHandle -eq 0) { throw 'WPF main window did not appear.' }
                    if (-not $ui.CloseMainWindow()) { throw 'Could not close the WPF main window.' }
                    if (-not $ui.WaitForExit(10000)) { throw 'WPF did not exit after closing its window.' }
                    if ($ui.ExitCode -ne 0) { throw "WPF exited with code $($ui.ExitCode)" }
                    Write-Host 'PASS normal WPF launch and close'
                }
                finally {
                    if (-not $ui.HasExited) { Stop-Process -Id $ui.Id -Force }
                }
            } else {
                throw 'RC smoke tests require an interactive Windows desktop; no release artifact will be uploaded.'
            }

            $uninstall = Get-ChildItem $testDir -Filter 'unins*.exe' | Select-Object -First 1
            if (-not $uninstall) { throw 'Uninstaller was not created.' }
            $process = Start-Process -FilePath $uninstall.FullName -ArgumentList @(
                '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru
            if ($process.ExitCode -ne 0) { throw "Uninstaller smoke test failed: $($process.ExitCode)" }
            if (Test-Path (Join-Path $testDir 'WGestures.Modern.exe')) {
                throw 'Application executable remains after uninstall.'
            }
            Write-Host 'PASS installer and uninstaller smoke test'
        }
        finally {
            if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
        }
    }

    @($setup, $portable, $source) | ForEach-Object {
        $hash = (Get-FileHash -Path $_ -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $([IO.Path]::GetFileName($_))"
    } | Set-Content $checksum -Encoding ascii
    Get-ChildItem $output -File | Select-Object Name, Length
    Get-Content $checksum
}
finally {
    Pop-Location
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
}
