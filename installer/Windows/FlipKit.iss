; FlipKit Hub Inno Setup Script
; Builds a Windows installer for FlipKit Hub (Desktop + Web + API)

#ifndef VERSION
  #define VERSION "3.3.6"
#endif

#define AppName "FlipKit"
#define Publisher "FlipKit"
#define ExeName "FlipKit.Desktop.exe"

[Setup]
AppId={{F8A2B3C4-D5E6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#VERSION}
AppVerName={#AppName} v{#VERSION}
AppPublisher={#Publisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=..\..\releases
OutputBaseFilename=FlipKit-Setup-v{#VERSION}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#ExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Start FlipKit when Windows starts"; GroupDescription: "Startup:"

[Files]
; Desktop application (single file)
Source: "..\..\releases\temp\FlipKit-Hub-Windows-x64-v{#VERSION}\{#ExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Servers folder
Source: "..\..\releases\temp\FlipKit-Hub-Windows-x64-v{#VERSION}\servers\*"; DestDir: "{app}\servers"; Flags: ignoreversion recursesubdirs createallsubdirs

; Documentation (optional - skip if not present)
Source: "..\..\releases\temp\FlipKit-Hub-Windows-x64-v{#VERSION}\Docs\*"; DestDir: "{app}\Docs"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\..\releases\temp\FlipKit-Hub-Windows-x64-v{#VERSION}\README.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\releases\temp\FlipKit-Hub-Windows-x64-v{#VERSION}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Tailscale guides from main Docs folder
Source: "..\..\Docs\Tailscale-Setup-Windows.md"; DestDir: "{app}\Docs"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\Docs\Tailscale-Setup-Mac.md"; DestDir: "{app}\Docs"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\Docs\Tailscale-Setup-Linux.md"; DestDir: "{app}\Docs"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\Docs\Mac-Installation-Guide.md"; DestDir: "{app}\Docs"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Registry]
; Auto-start on login (runs minimized)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FlipKit"; ValueData: """{app}\{#ExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#ExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\servers"
Type: filesandordirs; Name: "{app}\Docs"

; Code section removed - using skipifsourcedoesntexist flag instead
