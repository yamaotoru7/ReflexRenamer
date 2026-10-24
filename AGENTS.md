FlexRenamer\Flexible Renamer.exe を現在のツールキットを使ってクローンする。移植版の製品名は **Reflex Renamer**（exe は `ReflexRenamer.exe`、設定は `Reflex Renamer.ini`、版は csproj の `<Version>`、0.1.0 から）。見た目の方針は [docs/ui-redesign.md](docs/ui-redesign.md)。

## 前提

- `FlexRenamer/` にあるのはオリジナル版（Flexible Renamer v8.4、作者 Naru、2011年の Win32 MFC 製一括リネームツール。Strings.ini の Version は 8.0.0）の配布物一式で、これがリファレンス実装であり仕様の代わりになる。
- `FlexRenamer/` 配下は変更しない。オリジナル版を動かすときは、必ず作業コピー（`.work/sandbox/original/`）を使う。実行ディレクトリに `Flexible Renamer.ini` と `Rename.log` を書き込むため。
- `FlexRenamer/` は Git 管理外。新しいチェックアウトでは [docs/reference-setup.md](docs/reference-setup.md) に従って原本を別途配置する。採用した参照コピーのファイル一覧と SHA-256 は `docs/reference-files.json`。

## 技術スタック

- Windows 専用。C# / .NET 10 / WinForms。元の UI が Win32 標準の TreeView・ListView 構成なので、WinForms で見た目を寄せる。
- ソリューションは `ReflexRenamer.slnx`。
  - `src/ReflexRenamer.Core`：リネーム処理や設定ファイルの読み書きなど、UI に依存しない部分。ロジックはできるだけここに置いてテストする。
  - `src/ReflexRenamer`：WinForms の UI（`net10.0-windows`）。
  - `tests/ReflexRenamer.Core.Tests`：xUnit。`ReferenceFiles.Get("Preset.ini")` で `FlexRenamer/` 内のオリジナルのファイルを読み、互換性を確認する。
  - `tools/FrTools`：解析・自動操作・比較のツール（後述）。
- 方針として決めたこと：
  - VBScript は Microsoft が段階的に廃止しているため、スクリプト機能を VBScript だけに頼らない（現状は ActiveScript 経由で VBScript/JScript を使う `ScriptHost`）。
  - `StrFunc.dll` は 32bit COM で 64bit プロセスから読めないので、同等の機能をアプリ側で実装する。
  - 日時の曜日名・月名はオリジナル版と同じ Windows のロケールデータ（NLS）を使う（`Directory.Build.props` の `UseNls`）。

### コードの構成（src/ReflexRenamer）

- `MainForm`：メニュー、ツールバー1段（ファイル指定／フォルダ指定とフォルダ欄）、ページ切り替えタブ（`PageTabStrip`）、3ペイン（`ShellFolderTree`、入力フォーム、`FileListView`。`DockPane` は右端のドラッグで幅を変えるだけ）、ステータスバー。設定の保存・復元、フォルダ監視、ドロップ、お気に入り、言語、フォント。ダークモードは `Program` の `SetColorMode`（表示 > 配色、ini の ColorMode）、色は SystemColors から取る。言語ファイルにない文言は `AppResources.ExtraStrings` で訳す。
- `Pages/`：入力フォームの4ページ。`PageBase`（並べ替え・グループ・シャッフル）→ `RenamePageBase`（連番、実行する動作、実行と元に戻す、直接編集、オプション／スクリプトのメニュー）→ `RenamePage`（DIALOG 143 と定型メニュー `TypicalPanel`）／`TagRenamePage`（DIALOG 170）。`AttributePage`（141）、`NumberedObjectPage`（142）は `PageBase` 直下。
- `Dialogs/`：`TemplateForm` を継承したダイアログ（連番 166、重複 168、エラー 130、進行状況 133、プリセット整理 139、タグ・リネーム設定 172、オプション 237+238）。
- `Ui/`：`DialogBuilder`（テンプレートからの組み立て。DLU の基本値はフォントの実測）、`Anchors`、`PopupMenu`（Win32 メニュー）、`NativeUpDown`、`MruCombo`、`ShellInfo`、`ShellContextMenu`、`Assets`（オリジナルのアイコンと、移植版のアプリアイコン `app.ico`。再生成は `tools/make-app-icon.ps1`）、`FluentGlyphs`（Segoe Fluent Icons のグリフ）、`FlatToolStripRenderer`。
- Core 側：`Rename/`（`FileCollector` → `RenameEngine`（3段階の置換）→ `RenameExecutor`、`RenameLog`）、`Presets/`（Preset.ini、定型処理の展開、ユーザーのプリセット）、`Tags/`（各形式の読み取りと `TagFormatter`）、`Attributes/`、`NumberedObjects/`、`Settings/`（ini）、`Localization/`、`Scripting/`。

## コマンド

```sh
dotnet build                                   # 全体をビルド
dotnet test                                    # 全テスト
dotnet test --filter "FullyQualifiedName~PresetFileTests"   # 一部のテストだけ実行
dotnet run --project src/ReflexRenamer       # アプリを起動
dotnet run --project tools/FrTools             # ツールの使い方を表示
pwsh tools/build-installer.ps1                 # インストーラーを作る（.work/installer/ReflexRenamer-Setup-<版>.exe）
```

- インストーラーは Inno Setup 6（`winget install --id JRSoftware.InnoSetup -e --scope user`）。スクリプトは `installer/ReflexRenamer.iss`。.NET 同梱（win-x64、self-contained）で、管理者権限なしに `%LocalAppData%\Programs\Reflex Renamer` へ入れる（アプリが ini とログを exe の横に書くため）。版は csproj の `<Version>`。

## 仕様とタスク

- [docs/spec/features.md](docs/spec/features.md)：機能一覧（ID 付き）。情報源と検証方法も書いてある。
- [docs/spec/resources.md](docs/spec/resources.md)：exe から抽出した全ダイアログ（コントロール ID・座標・スタイル）、メニュー（コマンド ID）、アクセラレータ、ツールバー。`FrTools res-dump` で生成するので手で編集しない。
- [docs/tasks.md](docs/tasks.md)：作業順のタスクリスト。完了したらチェックを付け、完了条件に使ったシナリオやテスト名を書く。
- 新しい機能や挙動が見つかったら、features.md に ID を振って追記し、tasks.md にタスクを足す。

## 自動操作と比較（tools/FrTools）

オリジナル版と移植版を同じ手順で操作し、結果を比べる。どちらも UI Automation（FlaUI）で操作する。

```sh
dotnet run --project tools/FrTools -- sandbox                    # 作業コピーとフィクスチャを作り直す（最初に1回）
dotnet run --project tools/FrTools -- snap original              # 起動直後の画面と UI ツリーを取る
dotnet run --project tools/FrTools -- run <シナリオ> original     # シナリオを片方で実行
dotnet run --project tools/FrTools -- compare <シナリオ>          # 両方で実行して差分を出す（all で全シナリオ）
dotnet run --project tools/FrTools -- golden <セット>             # リネーム結果の正解データを取る（後述）
```

### 正解データ（リネーム処理の照合）

- `tools/FrTools/golden/<セット>.json` に検索・置換のケースを並べ、`golden <セット>` を実行すると、オリジナル版に1件ずつ入力してプレビュー一覧を読み、`tests/ReflexRenamer.Core.Tests/Golden/<セット>.json` に保存する。
- `GoldenRenameTests` がこの全ケースを Core のリネーム処理と照合する。挙動を調べたいときは、まずケースを足して正解データを取り、それに合わせて実装する。
- ケースごとの `steps` で変えた設定（オプションやチェック）は次のケースにも残る。使い終わったら戻すケースを置く。
- 正解データは日本語の Windows で取っているので、曜日名や日付書式が絡むケースは同じ環境でしか一致しない。

- 実行結果の照合は `exec-*` シナリオ（`exec-rename`、`exec-recycle`、`exec-attribute`、`exec-numbered`）。`files` ステップがフォルダの中身（名前、更新日時、作成日時、属性、サイズ。直近1時間の時刻は `(now)`）を書き出す。実行ボタンの後に出る確認メッセージは `window:Flexible Renamer` で木を取り、Enter で進める（セレクタの題名はオリジナル版のもので書く。移植版では `Sandbox.TitleFor` が「Reflex Renamer」に読み替える）。
- compare の合否は、一覧・メニュー・ファイル結果などテキストの記録の一致だけで決める（report.md 末尾の「合否」）。画像と UI ツリーの差は、見た目を Windows 11 風に変えたので参考扱い。
- 出力先は `.work/out/<シナリオ>/{original,clone,compare}/`。`compare/report.md` が概要。
  - `*.side.png`：左がオリジナル、中央が移植版、右が差分（赤が違う画素）。Read ツールで画像として見られる。
  - `*.tree.txt`：UI ツリーの差分（「移植版にない要素」「移植版にだけある要素」）。
  - `*.list.json.diff`：プレビュー一覧の行ごとの差分。オリジナル版の「新しい名前」の列が、リネーム処理の正解データになる。
- シナリオは `tools/FrTools/scenarios/*.json`。書式は `Scenario.cs` の先頭コメントを参照。`"only": "original"` で片方だけのステップを書ける。`"settings": { "セクション": { "キー": "値" } }` を付けると起動前に ini に書く（言語の確認 `lang-*.json` に使う）。
- `key` は既定で同時押し。メニューを ↓ → Enter とたどるときは `"each": true` を付ける。`menu` の最後の項目はマウスでクリックする（Invoke だとモーダルダイアログを開く項目で時間切れになる）。
- フィクスチャ（テスト用フォルダ）は `Sandbox.cs` の `Fixtures` に定義する。タイムスタンプも固定なので、何度作っても同じ状態になる。
- オリジナル版は毎回 ini なしの作業コピーから起動するので、前回の設定は残らない。ウィンドウは決まった位置と大きさ（`AppSession.MainBounds`）に揃える。
- 画面は OS の言語（日本語）で表示される。シナリオで名前を指定するときは日本語の表示名を使う。

### 移植版のコントロールに関する決まり

- 画面は exe から抽出したダイアログテンプレート（`docs/spec/resources.json`、埋め込み）から `DialogBuilder` で組み立てる。手でコントロールを置かない。`Control.Name` はオリジナルのリソース ID（例 `"1010"`）になり、UI Automation の AutomationId としてシナリオの `id:1010` が両方で使える。
- テンプレートは設計時の大きさ（DLU → ピクセル：横 7/4、縦 14/8）で作り、ペインいっぱいに広げる。広げたときの動きはコントロールごとのアンカー（`Ui/Anchors.cs`）で決める。アンカーは `tools/anchors.py` でオリジナル版の実測位置から求める。
- ダイアログは `TemplateForm` を継承し、ID 1 が OK、2 がキャンセル。
- メニュー項目とツールバーボタンの表示名は、オリジナル（Strings.ini の訳）と同じにする。
- 同じ ID が複数のダイアログで別の用途に使われている（1025 など）。ページごとに定数を持つ。

### 自動操作の注意

- テキスト入力は実際のキー入力で打ち込む（`setText`）。値を直接書き込むと変更通知が飛ばず、オリジナル版のプレビューが更新されない。入力後に値を読み戻して確かめる。
- オリジナル版のメニューは `command`（WM_COMMAND とコマンド ID）で実行するのが確実。移植版では `menu` を使う。ボタンから出るメニューは `popup`、中身の記録は `popupItems`。右クリックで出したメニューは `click` に `"button": "right"` を付けて開き、`contextItems`（記録）／`contextSelect`（選択）で扱う。
- コンボボックスの `select` は、オリジナル版ではドロップダウンの項目をクリックし、移植版ではキーボード（Home と ↓）で選ぶ。移植版の項目の位置は信用できず、クリックがツールバーに届いたことがある。
- リソース ID はダイアログ間で重複する（1025 は「高度なリネーム」チェックボックスでも、定型メニューの「置換前の文字列」でもある）。セレクタには `type:ComboBox` などを併記する。
- 移植版の WinForms コンボボックスは UI Automation の Items に項目が出ないので、ツールは `GetComboBoxInfo` でリスト窓を取って読む。ボタンから出すメニューは Win32 の `TrackPopupMenu`（`PopupMenu` クラス）で出す。`ContextMenuStrip` だと自動操作から見えず、見た目も違う。
- 前面に出せないとき（Xbox の `GameInputServiceWindow` が前面を持っていることがある）は、Alt キー、`AttachThreadInput`、`SwitchToThisWindow`、最小化→復元を順に試す。
- ボタンやラジオボタンはマウスでクリックする。UI Automation の Select／Invoke（MSAA の既定動作）を使うと、その後ポップアップメニューが開かなくなる。
- オーナードローの一覧（タグ・リネームの種類一覧 1088）は、項目のテキストの位置をマウスでクリックしないと切り替わらない。`click` に `"at": [dx, dy]`（対象の左上からの相対位置）を付ける。
- モーダルダイアログは UI Automation ではメインウィンドウの子として現れる。`window:タイトル` のセレクタはトップレベルとその直下のウィンドウを探す（`AppSession.AllWindows`）。
- ポップアップメニューで同じアクセスキーの項目が2つあるとき（MENU 173 の「タグ・リネーム(T)」と「タイムスタンプ(T)」）、キーを押すだけでは選択が移るだけなので、ツールは最後の項目で Enter を押す。
- 実行中はマウスとキーボードを使う。ユーザーが同時に PC を使っていてもよいように、次の仕組みがある。
  - 操作の前に必ず対象アプリを前面に出し、出せなければ入力しない（入力がユーザーのウィンドウに届くのを防ぐ）。
  - ユーザーの入力を検知したら、3秒間操作が止まるまで待つ（`AppSession.WaitForUserIdle`）。
  - ステップが失敗したら3回まで、シナリオや正解データ取り全体が失敗したら最初から3回までやり直す。
- Bash のヒアドキュメントではバックスラッシュが潰れることがある。`\1` などを含む JSON やコードは、ファイル編集ツールで書く。

## 移植の進め方

1. tasks.md から未完了のタスクを1つ選ぶ。
2. features.md と resources.md で仕様を確認する。足りなければ、シナリオを書いて `run <シナリオ> original` でオリジナル版の挙動を観察する。
3. 確認用のシナリオ（画面と、必要ならプレビュー一覧）とユニットテストを先に書く。
4. 実装し、`dotnet test` と `compare <シナリオ>` で差分がなくなるまで直す。
5. タスクにチェックを付ける。

## リファレンスファイルの読み方

- ほとんどの `.ini`（`Flexible Renamer.ini`、`Preset.ini`、`*.Strings.ini`）と `Rename.log` は **BOM 付き UTF-16 LE**（`TagDef.ini` だけは ASCII）。そのまま `cat` や `grep` すると文字化けするので、先に変換する。

  ```sh
  iconv -f UTF-16 -t UTF-8 "FlexRenamer/Preset.ini"
  ```

- `StrFunc/StrFunc.vbs` は Shift-JIS（`iconv -f SHIFT_JIS`）。`Script.Preset.js` は中身が空の UTF-16 ファイル。
- ヘルプ（`*.EN.chm`、`*.JA.chm`）は詳細仕様と変更履歴を含む。`7z x -o.work/help/ja "FlexRenamer/Flexible Renamer.JA.chm"` で展開すると、HTML 1枚に全内容が入っている。
- `Flexible Renamer.#.xls` は作者が管理していた翻訳の原本（CP932 の Excel）。`*.Strings.ini` はここから生成されている。

## 実装の際に間違えやすい点

- 正規表現エンジンは **boost regex（Perl 5 互換）**。.NET の Regex とは `\<` `\>`（語の先頭・末尾）、`\Z`、`\C` などが違う。
- 置換のタグ：`\f` はフォルダ名、`\F` は親フォルダ名、`\t` はファイルタイトル（タブではない）、`\e` は拡張子、`\0` は名前全体。全一覧は features.md の E05〜E07。
- ワイルドカードは大文字小文字を区別しない。拡張子のない名前（`README`）は `*.*` にマッチしない。連番は既定で 0 から始まる。
- `Preset.ini` の各行はタブ区切り。`[Preset]` は `Mode, Script, Search, Replace, Name`（Mode は 0 ワイルドカード、1 正規表現、2 文字変換）、`[Typical]` は `Param, Category, Mode, Script, Search, Replace, Name`。`Param` は使う入力欄（`\ADD` `\NUM` `\POS` `\NUMVALUE` `\TIMESTAMP` `\SRC` `\DST`）を示すビットマスク。
- プリセットや文字列中の `%%Key%%` は、使用中の `*.Strings.ini` の訳語に置き換える（`%%Copy%%` など）。ローカライズされた Windows でもマッチさせるため。
- `*.Strings.ini` は `[Language]`、`[Resource]`（`原文=訳文`）、プリセット名やタグ名の訳の順。キーは英語の原文。
