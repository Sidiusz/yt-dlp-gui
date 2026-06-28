; Compile with: ISCC.exe installer\Grabsy.iss
; Expects publish output at: Grabsy\bin\publish\win-x64

#define GrabsyName "Grabsy"
#ifndef GrabsyVersion
#define GrabsyVersion "1.0.0"
#endif
#define GrabsyPublisher "Sidiusz"
#define GrabsyURL "https://github.com/Sidiusz/yt-dlp-gui"
#define GrabsyExeName "Grabsy.exe"

#ifndef GrabsyPublishDir
  #define GrabsyPublishDir "..\Grabsy\bin\publish\win-x64"
#endif

[Setup]
AppId={{E5F4D9A0-9F4A-4B3D-9F5E-3B7C0E2B7F11}}
AppName={#GrabsyName}
AppVersion={#GrabsyVersion}
AppPublisher={#GrabsyPublisher}
AppPublisherURL={#GrabsyURL}
AppSupportURL={#GrabsyURL}/issues
AppUpdatesURL={#GrabsyURL}/releases
DefaultDirName={autopf}\{#GrabsyName}
DefaultGroupName={#GrabsyName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#GrabsyExeName}
UninstallDisplayName={#GrabsyName}
OutputDir=output
OutputBaseFilename=Grabsy-Setup-{#GrabsyVersion}
SetupIconFile=..\Grabsy\Assets\Icons\grabsy.ico
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
WizardStyle=modern
; In-app updater downloads the new setup and exits Grabsy before running it;
; CloseApplications covers the case where the app is still holding files.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startuplogin"; Description: "Run Grabsy at sign-in"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#GrabsyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#GrabsyName}"; Filename: "{app}\{#GrabsyExeName}"
Name: "{group}\Uninstall {#GrabsyName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#GrabsyName}"; Filename: "{app}\{#GrabsyExeName}"; Tasks: desktopicon

[Registry]
; Autostart is a highest-privilege scheduled task (see [Code]); a Run-key entry
; can't auto-elevate the app at login.

; WER LocalDumps: capture a full minidump even on native __fastfail
; (0xc0000409) crashes that bypass the in-app exception filter. Dumps land
; next to debug.log so a silent vanish always leaves post-mortem evidence.
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\{#GrabsyExeName}"; \
    ValueType: expandsz; ValueName: "DumpFolder"; ValueData: "%LOCALAPPDATA%\Grabsy\CrashDumps"; \
    Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\{#GrabsyExeName}"; \
    ValueType: dword; ValueName: "DumpType"; ValueData: "$00000002"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\{#GrabsyExeName}"; \
    ValueType: dword; ValueName: "DumpCount"; ValueData: "$00000005"

[Run]
Filename: "{app}\{#GrabsyExeName}"; Description: "{cm:LaunchProgram,{#GrabsyName}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Grabsy"

[Code]
const
  AutostartTaskName = 'GrabsyAutostart';

procedure CreateAutostartTask;
var
  ExePath, Params: string;
  ResultCode: Integer;
begin
  ExePath := ExpandConstant('{app}\{#GrabsyExeName}');
  // Inner-quote the /TR path or schtasks truncates it at the first space.
  Params := '/Create /TN "' + AutostartTaskName + '" /TR "\"' + ExePath + '\"" ' +
            '/SC ONLOGON /RU "' + ExpandConstant('{username}') + '" /RL HIGHEST /F';
  Exec(ExpandConstant('{sys}\schtasks.exe'), Params, '', SW_HIDE,
       ewWaitUntilTerminated, ResultCode);
end;

procedure DeleteAutostartTask;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\schtasks.exe'),
       '/Delete /TN "' + AutostartTaskName + '" /F', '', SW_HIDE,
       ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and IsTaskSelected('startuplogin') then
    CreateAutostartTask;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    DeleteAutostartTask;
end;









