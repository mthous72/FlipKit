; FlipKit Inno Setup Script
; Builds a Windows installer for FlipKit Hub

#ifndef VERSION
  #define VERSION "3.3.0"
#endif

[Setup]
AppId={{F8A2B3C4-D5E6-7890-ABCD-EF1234567890}
AppName=FlipKit
AppVersion={#VERSION}
AppVerName=FlipKit v{#VERSION}
AppPublisher=FlipKit
AppPublisherURL=https://github.com/your-repo/flipkit
AppSupportURL=https://github.com/your-repo/flipkit/issues
DefaultDirName={autopf}\FlipKit
DefaultGroupName=FlipKit
AllowNoIcons=yes
LicenseFile=..\..\LICENSE
OutputDir=..\..\installers
OutputBaseFilename=FlipKit-Setup-Windows-x64-v{#VERSION}
SetupIconFile=..\..\FlipKit.Desktop\Assets\flipkit.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start FlipKit when Windows starts"; GroupDescription: "Startup:"

[Files]
; Main application files
Source: "..\..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Documentation
Source: "..\..\Docs\Mac-Installation-Guide.md"; DestDir: "{app}\Docs"; Flags: ignoreversion
Source: "..\..\Docs\Tailscale-Setup-Windows.md"; DestDir: "{app}\Docs"; Flags: ignoreversion
Source: "..\..\Docs\Tailscale-Setup-Mac.md"; DestDir: "{app}\Docs"; Flags: ignoreversion
Source: "..\..\Docs\Tailscale-Setup-Linux.md"; DestDir: "{app}\Docs"; Flags: ignoreversion

[Icons]
Name: "{group}\FlipKit"; Filename: "{app}\FlipKit.Desktop.exe"
Name: "{group}\{cm:UninstallProgram,FlipKit}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\FlipKit"; Filename: "{app}\FlipKit.Desktop.exe"; Tasks: desktopicon

[Registry]
; Auto-start on login
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FlipKit"; ValueData: """{app}\FlipKit.Desktop.exe"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\FlipKit.Desktop.exe"; Description: "{cm:LaunchProgram,FlipKit}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Any post-install tasks can go here
  end;
end;
