#define AppName "SnipArc"
#define AppExeName "ScreenCaptureApp.exe"
#define AppVersion "0.2.0"
#define AppPublisher "77degrees"
#ifndef PublishDir
  #define PublishDir "..\artifacts\app\win-x64"
#endif

[Setup]
AppId={{8A52C949-D9EC-4329-A0E1-CFEA23EB9464}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\ScreenCaptureApp
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=SnipArc-Setup-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\app-icon.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installer
VersionInfoProductName={#AppName}
MinVersion=10.0.22000

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[InstallDelete]
; Remove shortcuts created under the pre-SnipArc alpha display name during an in-place upgrade.
Type: files; Name: "{autodesktop}\Screen Capture App.lnk"
Type: filesandordirs; Name: "{userprograms}\Screen Capture App"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Registry]
; The application owns its optional HKCU Run value. Remove it during uninstall even when settings are preserved.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "ScreenCaptureApp"; Flags: uninsdeletevalue

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
