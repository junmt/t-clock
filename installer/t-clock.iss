; t-clock インストーラー (Inno Setup 6)
;
; CI からは次のように呼び出す:
;   iscc /DMyAppVersion=1.2.3 /DSourceExe=..\publish\t-clock.exe installer\t-clock.iss
; 生成物: installer\Output\t-clock-<version>-setup.exe

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\publish\t-clock.exe"
#endif

#define MyAppName "t-clock"
#define MyAppExeName "t-clock.exe"

[Setup]
; AppId はアップグレード判定に使うため変更しないこと
AppId={{14698F2D-7F2A-4C13-9AC4-BA4F4D8EBD4B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=junmt
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; 既定はユーザー単位インストール (管理者権限不要)。ダイアログで全ユーザー向けも選べる
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; 起動中の t-clock を Restart Manager で閉じてから上書きする
CloseApplications=yes

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:AutostartGroup}"

[CustomMessages]
japanese.AutostartGroup=起動オプション:
japanese.AutostartTask=Windows の起動時に t-clock を自動起動する
english.AutostartGroup=Startup options:
english.AutostartTask=Start t-clock automatically when Windows starts

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; 「自動起動」タスク選択時はアプリの ⚡ ボタンと同じ Run キーへ登録する
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "t-clock"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue
; アプリの「自動起動」で登録される Run キーをアンインストール時に削除する (作成はしない)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "t-clock"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
; アプリ内アップデート (/SILENT /UPDATE で起動される) の完了後は自動で再起動する
Filename: "{app}\{#MyAppExeName}"; Flags: nowait; Check: IsUpdateMode

[Code]
function IsUpdateMode: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/UPDATE') = 0 then
      Result := True;
end;
