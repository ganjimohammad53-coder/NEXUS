#define AppName "NEXUS Personal Control Center"
#define AppVersion "1.0"
#define AppPublisher "NEXUS"
#define AppExeName "NEXUS.exe"

[Setup]
AppId={{D47E2A73-26F6-4F6C-8B72-0BBA5F2F1B10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\NEXUS
DefaultGroupName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=NEXUS-1.0-Setup-x64
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\artifacts\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\NEXUS"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\NEXUS"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch NEXUS"; Flags: postinstall nowait skipifsilent
