#define MyAppName "APODWallpaper"
#define MyConfiguratorName "APODWallpaper Configurator"
; MyAppVersion and MyAppSourceDir may be supplied by CI via ISCC /D defines;
; fall back to local defaults when building by hand.
#ifndef MyAppVersion
  #define MyAppVersion "2026.03.09.1"
#endif
#ifndef MyAppSourceDir
  #define MyAppSourceDir "C:\Users\alexj\VSCode\C#\APODWallpaper"
#endif
; The git repo root (holds LICENSE). Locally it is the APODWallpaper subfolder
; of the source dir; in CI the checkout root IS the source dir.
#ifndef MyRepoDir
  #define MyRepoDir MyAppSourceDir + "\APODWallpaper"
#endif
#define MyAppPublisher "TransparentBox"
#define MyAppExeName "APODWallpaper.exe"
#define MyConfiguratorExeName "ConfiguratorGUI.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{D67B5BBA-CA8D-4F44-8FAB-5725D0604D50}
AppName={#MyAppName}
AppVersion={#MyAppVersion}  
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://www.transparentbox.uk
AppComments={#MyAppName} v{#MyAppVersion}
AppCopyright=Copyright (C) 2024 Alexander Johnson
AppReadmeFile=https://github.com/alex-ljohnson/APODWallpaper/blob/main/README.md
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=lowest
SourceDir={#MyAppSourceDir}
OutputDir={#MyAppSourceDir}\setup
OutputBaseFilename=APODWallpaper_Setup_v{#MyAppVersion}
LicenseFile={#MyRepoDir}\LICENSE
Compression=lzma2/max
SolidCompression=no
WizardStyle=modern dynamic

ChangesAssociations=yes
ArchitecturesAllowed=win64
MinVersion=10.0
; Skip code signing in CI (no certificate available on the runner).
#ifndef CI_NO_SIGN
SignTool=standard
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "Full"; Description: "Full installation"
Name: "Lightweight"; Description: "Lightweight installation (only required components)"
Name: "Custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "APODWallpaper"; Description: "APODWallpaper Main Program"; Types: Full Lightweight Custom; Flags: fixed
Name: "Configurator"; Description: "Configurator Interface"; Types: Full Custom


[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Components: Configurator; GroupDescription: "{cm:AdditionalIcons}"
Name: "starticon"; Description: "Create a start menu shortcut"; Components: Configurator

[Files]
; Standalone
Source: "dist\{#MyAppExeName}"; DestDir: "{app}"; Components: APODWallpaper; Flags: ignoreversion
Source: "dist\APODWallpaper.deps.json"; DestDir: "{app}"; Components: APODWallpaper; Flags: ignoreversion
Source: "dist\APODWallpaper.dll"; DestDir: "{app}"; Components: APODWallpaper; Flags: ignoreversion
Source: "dist\APODWallpaper.pdb"; DestDir: "{app}"; Components: APODWallpaper; Flags: ignoreversion
Source: "dist\APODWallpaper.runtimeconfig.json"; DestDir: "{app}"; Components: APODWallpaper; Flags: ignoreversion
; Configurator
Source: "dist\{#MyConfiguratorExeName}"; DestDir: "{app}"; Components: Configurator; Flags: ignoreversion
Source: "dist\ConfiguratorGUI.deps.json"; DestDir: "{app}"; Components: Configurator; Flags: ignoreversion
Source: "dist\ConfiguratorGUI.dll"; DestDir: "{app}"; Components: Configurator; Flags: ignoreversion
Source: "dist\ConfiguratorGUI.pdb"; DestDir: "{app}"; Components: Configurator; Flags: ignoreversion
Source: "dist\ConfiguratorGUI.runtimeconfig.json"; DestDir: "{app}"; Components: Configurator; Flags: ignoreversion
; Shared
Source: "dist\Microsoft.Extensions.*.dll"; DestDir: "{app}"; Components: APODWallpaper Configurator; Flags: ignoreversion
Source: "dist\Newtonsoft.Json.dll"; DestDir: "{app}"; Components: APODWallpaper Configurator; Flags: ignoreversion
;Docs
Source: "dist\Resources\help.html"; DestDir: "{app}\Resources"; Flags: ignoreversion
Source: "dist\README.md"; DestDir: "{app}\info"; Flags: isreadme
Source: "{#MyRepoDir}\LICENSE"; DestDir: "{app}\info";

; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyConfiguratorName}"; Filename: "{app}\{#MyConfiguratorExeName}"; Components: Configurator; Tasks: starticon
Name: "{autodesktop}\{#MyConfiguratorName}"; Filename: "{app}\{#MyConfiguratorExeName}"; Components: Configurator; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "APODWallpaper"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletevalue 

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Components: APODWallpaper; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#MyConfiguratorExeName}"; Description: "{cm:LaunchProgram,{#StringChange('the Configurator', '&', '&&')}}"; Components: Configurator; Flags: nowait postinstall

