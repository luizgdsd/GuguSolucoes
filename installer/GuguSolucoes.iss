#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#ifndef MyPublishDir
#define MyPublishDir ".\\dist\\publish"
#endif

#ifndef MyOutputDir
#define MyOutputDir ".\\dist"
#endif

#ifndef MySetupIcon
#define MySetupIcon "..\\favicon.ico"
#endif

#define MyAppName "GuguSolucoes"
#define MyAppPublisher "Gugu Solucoes"
#define MyAppExeName "GuguSolucoes.exe"
#define MyAgentTaskName "GuguSolucoes TempCleanup Agent"

[Setup]
AppId={{3D4A4EB4-8E33-4364-90BA-A4B5B66B4D2F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\GuguSolucoes
DefaultGroupName=GuguSolucoes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
OutputDir={#MyOutputDir}
OutputBaseFilename=GuguSolucoes-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#MySetupIcon}
ArchitecturesAllowed=x64 arm64 x86
ArchitecturesInstallIn64BitMode=x64 arm64
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
SetupMutex=GuguSolucoesInstallerMutex

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; GroupDescription: "Atalhos:"; Flags: checkedonce
Name: "startupicon"; Description: "Iniciar GuguSolucoes com o Windows"; GroupDescription: "Atalhos:"; Flags: checkedonce
Name: "autocleanup"; Description: "Ativar limpeza automatica (startup)"; GroupDescription: "LimpaCache:"; Flags: unchecked

[Dirs]
Name: "{commonappdata}\LimpaCache"; Permissions: users-modify
Name: "{commonappdata}\LimpaCache\logs"; Permissions: users-modify

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\GuguSolucoes"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\GuguSolucoes"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\GuguSolucoes"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: startupicon; Parameters: "--tray"

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Unregister-ScheduledTask -TaskName 'LimpaCache Agent' -Confirm:$false -ErrorAction SilentlyContinue"""; Flags: runhidden waituntilterminated
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Unregister-ScheduledTask -TaskName '{#MyAgentTaskName}' -Confirm:$false -ErrorAction SilentlyContinue"""; Flags: runhidden waituntilterminated
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""$action = New-ScheduledTaskAction -Execute '{app}\{#MyAppExeName}' -Argument '--agent'; $trigger = New-ScheduledTaskTrigger -AtStartup; Register-ScheduledTask -TaskName '{#MyAgentTaskName}' -Action $action -Trigger $trigger -User 'SYSTEM' -RunLevel Highest -Force"""; Flags: runhidden waituntilterminated; Tasks: autocleanup
Filename: "{app}\{#MyAppExeName}"; Parameters: "--updated"; WorkingDir: "{app}"; Flags: nowait; Check: ShouldRestartAfterUpdate
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir GuguSolucoes"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Stop-ScheduledTask -TaskName '{#MyAgentTaskName}' -ErrorAction SilentlyContinue"""; Flags: runhidden; RunOnceId: "GuguSolucoes_StopTask"
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Unregister-ScheduledTask -TaskName '{#MyAgentTaskName}' -Confirm:$false -ErrorAction SilentlyContinue"""; Flags: runhidden; RunOnceId: "GuguSolucoes_UnregisterTask"
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Unregister-ScheduledTask -TaskName 'LimpaCache Agent' -Confirm:$false -ErrorAction SilentlyContinue"""; Flags: runhidden; RunOnceId: "GuguSolucoes_UnregisterLegacyTask"

[Code]
function ShouldRestartAfterUpdate: Boolean;
begin
  Result := ExpandConstant('{param:GUGU_RESTART|0}') = '1';
end;
