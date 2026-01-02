; Win7 Virtual Monitor Installer Script
; Inno Setup Script - Download Inno Setup from https://jrsoftware.org/isinfo.php

#define MyAppName "Win7 Virtual Monitor"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Win7App"
#define MyAppExeName "Win7App.exe"

[Setup]
; App Info
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL=https://github.com/win7app
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\output
OutputBaseFilename=Win7VirtualMonitor_Setup_v{#MyAppVersion}
SetupIconFile=..\Win7App\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Windows 7 minimum
MinVersion=6.1

; Architecture - 32-bit app can run on both
ArchitecturesAllowed=x86 x64
ArchitecturesInstallIn64BitMode=

; Privileges
PrivilegesRequired=admin

; Uninstaller
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "indonesian"; MessagesFile: "compiler:Languages\Indonesian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Startup:"

[Files]
; Main executable
Source: "..\Win7App\bin\Release\Win7App.exe"; DestDir: "{app}"; Flags: ignoreversion

; Certificate files if any
Source: "..\Win7App\bin\Release\*.pfx"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\Win7App\bin\Release\*.cer"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
; Optionally run after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Add firewall exception
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules"; ValueType: string; ValueName: "{#MyAppName}_TCP_8080"; ValueData: "v2.10|Action=Allow|Active=TRUE|Dir=In|Protocol=6|LPort=8080|App={app}\{#MyAppExeName}|Name={#MyAppName} HTTP|"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules"; ValueType: string; ValueName: "{#MyAppName}_TCP_8081"; ValueData: "v2.10|Action=Allow|Active=TRUE|Dir=In|Protocol=6|LPort=8081|App={app}\{#MyAppExeName}|Name={#MyAppName} HTTPS|"; Flags: uninsdeletevalue

[Code]
// Show firewall warning
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin
    MsgBox('Installation complete!' + #13#10 + #13#10 +
           'The app will listen on:' + #13#10 +
           '  - HTTP:  Port 8080' + #13#10 +
           '  - HTTPS: Port 8081' + #13#10 + #13#10 +
           'Firewall rules have been added automatically.', mbInformation, MB_OK);
  end;
end;
