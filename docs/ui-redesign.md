# UI 見直し（Reflex Renamer への改名と Windows 11 風の見た目）

2026-10-24 に決めた方針。レビューの経緯と判断材料は作業時のページにまとめた（判断は下の「決定」のとおり）。
この文書は実装する人（人・エージェント）が共通に守る前提。

## 決定

- 名称は **Reflex Renamer**。版は **0.1.0** から始める（csproj の `Version`。画面・バージョン情報・インストーラーに出す版もこれ）。
- 方向は **A：土台の入れ替え**。画面の構造（テンプレート駆動の `DialogBuilder` と `Anchors`、コントロールの `Name` ＝ リソース ID）は残し、フォント・DPI・テーマ・描画・余白・アイコンを差し替える。ページを `TableLayoutPanel` で組み直す「B」は、A が終わってから必要なら。
- **ダークモード**に対応する（`Application.SetColorMode`）。既定は OS に従い、表示 > 配色 でライト／ダークに固定できる（ini の `ColorMode`、反映は次回起動から）。追従しない部品は個別に直す。
- 4 ページの切り替えは **タブ**にする。ツールバーは「ファイル指定／フォルダ指定 ＋ フォルダ欄」の 1 段にまとめる。
- ペインの **並べ替え・浮かせる機能（MFC のドッキング）はやめる**。残すのは幅の変更と「フォルダ一覧を隠す」。
- アイコンは、ツールバーとページ見出しを Segoe Fluent Icons のグリフに差し替える。定型メニューのツリーの絵（311〜316、310）はそのまま。
- オリジナル版の作者・配布元への導線（ヘルプ > ホームページを開く、入力サポートメニューの「メールで質問」、バージョン情報の URL とメール）は除く（`Commands.Removed` を `ResourceSet.WithoutCommands` でメニューから外す）。クレジットはバージョン情報とヘルプの「Inspired by Flexible Renamer by Naru」の一文だけ。
- ヘルプは CHM ではなく HTML（`src/ReflexRenamer/Help/{ja,en}/index.html`、出力の `help\`）。日本語の UI では日本語版、ほかの言語では英語版を既定のブラウザーで開く。
- compare（FrTools）の合否は、一覧の中身（`*.list.json`）とファイルの結果（`files`）の一致だけにする。画面（画像・UI ツリーの座標）の差は参考扱い。
- 一覧の「フォルダ」列は、フォルダ欄のフォルダからの **相対パス**にする（直下は `.`。ファイル指定のときと配下でないものはフルパス。`FolderDisplay.Relative`）。オリジナル版はフルパス。compare はフォルダ列を両方とも同じ規則で相対パスにそろえてから比べる。

## 実装の決まり

- コントロールの `Name`（リソース ID、例 `"1010"`）と `AccessibleName` は変えない。自動操作（FrTools）がこれで要素を探す。
- `FlatStyle.System` は維持する（OS のテーマとダークモードに追従させるため）。`CreateParams` で Win32 スタイルを調整している部分も残す。
- ボタンから出すメニューは Win32 の `TrackPopupMenu`（`PopupMenu`）のまま。`ContextMenuStrip` にしない。
- テンプレートの座標（DLU）は変えない。DLU → ピクセルの基本値は、実際のフォントの平均文字幅と高さから求める（Win32 の `GetDialogBaseUnits` と同じ考え方）。
- 既定フォントは `SystemFonts.MessageBoxFont`（日本語 Windows では Yu Gothic UI 9pt）。ユーザーが「表示 > フォント」で変えた値は今までどおり ini の `Font`／`FontFace` に保存する。
- 高 DPI は `PerMonitorV2`。フォームは `AutoScaleMode.Dpi`。
- データファイルはオリジナル版の配布物から `src/ReflexRenamer/Data/` に取り込み、製品として直したものを使う（2026-10-25）。言語ファイルは `Reflex Renamer.<言語>.Strings.ini`（キーは英語の原文のまま、訳文の「Flexible Renamer」を「Reflex Renamer」に。ホームページ・メールの行は削除）、`Preset.ini`・`TagDef.ini`・`Script.Preset.*` は内容そのまま。設定ファイルは `Reflex Renamer.ini`、ログは `Rename.log`。ini のセクション名・キー名はオリジナル版と同じにするが、固有名詞だった `[Flexible Renamer]`（アプリ全体の設定）だけは `[General]` にする（`AppSettings.General`。古いセクションは読み込み時に移す）。
- 文字列はこれまでどおり `AppResources.T()` で言語ファイルから引く。新しく足す文言は、言語ファイルにキーがなければ日本語 Windows でそのまま出てよいが、英語の原文をキーにして `T()` を通す。
- コードのコメントは日本語。ファイルは UTF-8、改行は CRLF（既存ファイルに合わせる）。
- `FlexRenamer/` 配下は変更しない。

## 直す不具合（スクリーンショットで確認したもの）

1. 重複確認ダイアログの「処理」の中身が GroupBox に隠れる → 修正済み（`DialogBuilder.Build` で全部足した後に枠を奥へ送る）。
2. ステータスバーの件数欄（幅 100px 固定）で数が切れる。
3. 一覧の右 4 列（サイズ・種類・更新日時・作成日時）が 32px で読めない。フォルダ列が幅の半分。
4. フォルダツリーが旧スタイル（＋と点線）。ファイル一覧も含めて Explorer テーマに。
5. オプションの見出しアイコンが出ない（存在しない ID 128 を参照。正しくは 64）。UAC の盾、「送る」一覧のページアイコンも出ていない。
6. エラーダイアログが例外の内容（detail）を捨てている。長い名前が切れる。
7. フォルダ欄のテキストが末尾側にスクロールして先頭の「D」が隠れることがある。
8. 連番オブジェクトページの一覧に横スクロールバーが出る。
9. 名前欄の「<ここに名前を入力してください>」を文字として入れている → `PlaceholderText`。
10. 定型メニューのツリーの長い項目が右端で切れる（太字 + 横スクロール禁止）。

## 進め方（2026-10-24 に 1〜5 を実施。残りは tasks.md の T908）

1. 不具合 2〜10、Explorer テーマ、変更行のハイライト（agy 第 1 便）— 済
2. システムフォント、DLU のフォント実測、PerMonitorV2、ステータスバー・メニューの自動サイズ、下線の強制表示をやめる、名称と版、ダークモードの土台（agy 第 2 便）— 済
3. タブ化（`PageTabStrip`）と 1 段ツールバー、アイコンのグリフ化（`FluentGlyphs`）、ドッキング機能の廃止（agy 第 3 便）— 済
4. ダークモードで追従しない部品の修正（くぼみ線、無効なテキストボックス、ページ内の一覧、ラジオボタン、ツールバーの `FlatToolStripRenderer`）（Claude）— 済
5. FrTools の合否基準の変更、docs と AGENTS.md の更新（Claude）— 済。インストーラーのビルド確認は未

## 実装してみて分かったこと

- WinForms のダークモードで追従しなかった部品：`ToolStrip`（既定・System どちらのレンダラーも明るいまま → SystemColors で塗る自前レンダラー）、無効な `TextBox`（白いまま → EnabledChanged で `SystemColors.Control`）、ページ内の `ListBox`／`ListView`（→ `SetWindowTheme("Explorer")`）、`FlatStyle.System` の `RadioButton`（文字が灰色 → Standard で描く）、`ControlPaint.DrawBorder3D`（白く光る → SystemColors のペンで線を引く）、`msctls_updown32`（未対応）。
- 一覧の列幅は、狭いときにフォルダ → 種類 → 日時の順に最低幅まで縮め、名前の 2 列に 120px ずつ確保する（`FileListView.Shrink`）。
- `Controls.Add` は後から足したものほど奥になる。テンプレートの GroupBox は中身より先に並ぶので、全部足し終えてから枠を奥へ送る。
