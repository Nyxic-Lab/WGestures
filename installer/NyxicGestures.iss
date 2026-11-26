#ifndef PayloadDir
  #error PayloadDir must point to the self-contained publish directory
#endif
#ifndef OutputDir
  #error OutputDir must point to the installer output directory
#endif
#ifndef AppVersion
  #define AppVersion "0.1.0-rc.4"
#endif

[Setup]
AppId={{D662E33A-419B-4E53-A07B-C2C63DA8D136}
AppName=Nyxic Gestures
AppVersion={#AppVersion}
AppPublisher=Nyxic Lab
AppPublisherURL=https://github.com/Nyxic-Lab/WGestures
AppSupportURL=https://github.com/Nyxic-Lab/WGestures/issues
DefaultDirName={localappdata}\Programs\Nyxic Gestures
DefaultGroupName=Nyxic Gestures
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.14393
OutputDir={#OutputDir}
OutputBaseFilename=NyxicGestures-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\WGestures.Modern.exe
CloseApplications=yes
RestartApplications=no
LicenseFile={#PayloadDir}\LICENSE.txt
InfoBeforeFile={#PayloadDir}\RELEASE-NOTES.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Nyxic Gestures"; Filename: "{app}\WGestures.Modern.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Nyxic Gestures"; Filename: "{app}\WGestures.Modern.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\WGestures.Modern.exe"; Description: "Launch Nyxic Gestures"; Flags: nowait postinstall skipifsilent

; Deliberately leave the user's independent workspace and the original
; WGestures installation and configuration untouched on uninstall.

[Code]
function StopExistingEngine(): Boolean;
var
  ExitCode: Integer;
  EnginePath: String;
begin
  Result := True;
  EnginePath := ExpandConstant('{app}\engine\Nyxic.Gestures.Engine.exe');
  if FileExists(EnginePath) then
    Result := Exec(EnginePath, '--stop', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) and (ExitCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopExistingEngine() then
    Result := 'Please exit Nyxic Gestures from its system tray before installing.';
end;

function InitializeUninstall(): Boolean;
begin
  Result := StopExistingEngine();
  if not Result then
    MsgBox('Please exit Nyxic Gestures from its system tray before uninstalling.', mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'NyxicGestures');
end;
