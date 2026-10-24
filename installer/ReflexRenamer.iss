; Flexible Renamer（移植版）のインストーラー。tools/build-installer.ps1 から ISCC で作る。
;
; - アプリは設定（Flexible Renamer.ini）とログ（Rename.log）を exe と同じフォルダに書く（オリジナル版と同じ）。
;   そのため管理者権限なしで、ユーザーのフォルダ（%LocalAppData%\Programs）に入れる。
; - .NET は同梱する（self-contained の発行物）。利用者側で .NET のインストールは要らない。
; - 上書きインストールやアンインストールでは、ini とログを消さない（インストーラーが入れたファイルではないため）。

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

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
