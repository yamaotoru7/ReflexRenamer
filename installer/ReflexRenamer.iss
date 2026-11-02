; Reflex Renamer のインストーラー。tools/build-installer.ps1 から ISCC で作る。
;
; - アプリは設定（Reflex Renamer.ini）とログ（Rename.log）を exe と同じフォルダに書く。
;   そのため管理者権限なしで、ユーザーのフォルダ（%LocalAppData%\Programs）に入れる。
; - .NET は同梱する（self-contained の発行物）。利用者側で .NET のインストールは要らない。
; - 上書きインストールやアンインストールでは、ini とログを消さない（インストーラーが入れたファイルではないため）。
; - アンインストールでは、オプションで登録したフォルダの右クリックメニューと「送る」の項目を取り除く。

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\.work\publish"
#endif

#define AppName "Reflex Renamer"
#define AppExe "ReflexRenamer.exe"

[Setup]
AppId={{7CAF7B69-C277-4A43-B036-92B4C6FCE239}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\.work\installer
OutputBaseFilename=ReflexRenamer-Setup-{#AppVersion}
SetupIconFile=..\src\ReflexRenamer\Assets\Icons\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 発行物をそのまま入れる。設定とログは実行時に作られるものなので入れない
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,Reflex Renamer.ini,Rename.log"; Flags: ignoreversion recursesubdirs createallsubdirs
; ライセンス（MIT。配布物に同梱する条件のため）
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; オプション（全般）で登録するフォルダの右クリックメニュー。インストールでは作らず、アンインストールで消すだけ
Root: HKCU; Subkey: "Software\Classes\Folder\shell\Reflex Renamer"; Flags: dontcreatekey uninsdeletekey

[UninstallDelete]
; オプション（全般）で登録する「送る」の項目（Reflex Renamer (リネーム) など）
Type: files; Name: "{usersendto}\Reflex Renamer (*).lnk"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
