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


[Code]
const
  // Old default key, shared by every install so it kept running out
  LegacySharedKey = '5zgCnpExBIpD6hZvruRRJS48WfKYBe0PlVVaO5NZ';
  SignupUrl = 'https://api.nasa.gov/';

var
  ApiKeyPage: TInputQueryWizardPage;

// Pulls "API_KEY" out of an existing config.json, a full JSON parser isn't worth it here
function ReadExistingApiKey(): String;
var
  Json: AnsiString;
  S: String;
  P: Integer;
begin
  Result := '';
  if not LoadStringFromFile(ExpandConstant('{userappdata}\APODWallpaper\config.json'), Json) then Exit;
  S := String(Json);
  P := Pos('"API_KEY"', S);
  if P = 0 then Exit;
  Delete(S, 1, P + Length('"API_KEY"') - 1);
  S := TrimLeft(S);
  if Copy(S, 1, 1) <> ':' then Exit;
  Delete(S, 1, 1);
  S := TrimLeft(S);
  // null or anything else that isn't a string
  if Copy(S, 1, 1) <> '"' then Exit;
  Delete(S, 1, 1);
  P := Pos('"', S);
  if P = 0 then Exit;
  Result := Trim(Copy(S, 1, P - 1));
  if Result = LegacySharedKey then Result := '';
end;

procedure SignupLinkClick(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', SignupUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure InitializeWizard();
var
  SignupLink: TNewStaticText;
begin
  ApiKeyPage := CreateInputQueryPage(wpSelectTasks,
    'NASA API key', 'Pictures come from NASA''s free API',
    'NASA doesn''t require a key at the moment but may in future. Adding one now avoids interruptions, ' +
    'and a free personal key takes about a minute to get.' + #13#10#13#10 +
    'Paste your key below, or leave it blank to use the demo key. You can change it later in the Configurator.');
  ApiKeyPage.Add('&API key:', False);
  ApiKeyPage.Values[0] := ReadExistingApiKey();

  SignupLink := TNewStaticText.Create(ApiKeyPage);
  SignupLink.Parent := ApiKeyPage.Surface;
  SignupLink.Caption := 'Get a free key at api.nasa.gov';
  SignupLink.Left := ApiKeyPage.Edits[0].Left;
  SignupLink.Top := ApiKeyPage.Edits[0].Top + ApiKeyPage.Edits[0].Height + ScaleY(10);
  SignupLink.Cursor := crHand;
  SignupLink.Font.Style := [fsUnderline];
  SignupLink.OnClick := @SignupLinkClick;
end;

function IsAlphaNumeric(const S: String): Boolean;
var
  I: Integer;
  C: Char;
begin
  Result := True;
  for I := 1 to Length(S) do
  begin
    C := S[I];
    if not (((C >= 'a') and (C <= 'z')) or ((C >= 'A') and (C <= 'Z')) or ((C >= '0') and (C <= '9'))) then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Key: String;
begin
  Result := True;
  if CurPageID <> ApiKeyPage.ID then Exit;
  Key := Trim(ApiKeyPage.Values[0]);
  ApiKeyPage.Values[0] := Key;
  // NASA keys are 40 letters and digits, anything else is probably a bad paste
  if (Key <> '') and ((Length(Key) <> 40) or not IsAlphaNumeric(Key)) then
    Result := MsgBox('That doesn''t look like a NASA API key, they are 40 letters and numbers.' + #13#10#13#10 +
      'Use it anyway?', mbConfirmation, MB_YESNO) = IDYES;
end;

function GetApiKey(): String;
begin
  Result := Trim(ApiKeyPage.Values[0]);
  // Would break the command line quoting
  StringChangeEx(Result, '"', '', True);
end;

// Runs before the Finished page so the postinstall launches see the key
// Silent installs (the Configurator's updater) never showed the page so leave the key alone
procedure CurStepChanged(CurStep: TSetupStep);
var
  Key, Fallback: String;
  ResultCode: Integer;
begin
  if (CurStep <> ssPostInstall) or WizardSilent() then Exit;
  Key := GetApiKey();
  WizardForm.StatusLabel.Caption := 'Saving NASA API key...';
  if (not Exec(ExpandConstant('{app}\{#MyAppExeName}'), '--set-key "' + Key + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
      or (ResultCode <> 0)) and (Key <> '') then
  begin
    // Lightweight installs have no Configurator to fall back on
    if WizardIsComponentSelected('Configurator') then
      Fallback := 'You can add it later in the Configurator under Settings.'
    else
      Fallback := 'You can add it later by running this installer again.';
    MsgBox('Your NASA API key couldn''t be saved. ' + Fallback, mbError, MB_OK);
  end;
end;
