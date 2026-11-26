#ifndef PayloadDir
  #error PayloadDir must point to the self-contained publish directory
#endif
#ifndef OutputDir
  #error OutputDir must point to the installer output directory
#endif
#ifndef AppVersion
  #define AppVersion "0.1.0-preview.1"
#endif

[Setup]
AppId={{D662E33A-419B-4E53-A07B-C2C63DA8D136}
AppName=Nyxic Gestures Preview
AppVersion={#AppVersion}
AppPublisher=Nyxic Lab
AppPublisherURL=https://github.com/Nyxic-Lab/WGestures
AppSupportURL=https://github.com/Nyxic-Lab/WGestures/issues
DefaultDirName={localappdata}\Programs\Nyxic Gestures Preview
DefaultGroupName=Nyxic Gestures Preview
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=NyxicGestures-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\WGestures.Modern.exe
CloseApplications=yes
RestartApplications=no
LicenseFile={#PayloadDir}\LICENSE.txt
InfoBeforeFile={#PayloadDir}\PREVIEW.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Nyxic Gestures Preview"; Filename: "{app}\WGestures.Modern.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Nyxic Gestures Preview"; Filename: "{app}\WGestures.Modern.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\WGestures.Modern.exe"; Description: "Launch Nyxic Gestures Preview"; Flags: nowait postinstall skipifsilent

; Deliberately leave the user's independent workspace and the original
; WGestures installation and configuration untouched on uninstall.
