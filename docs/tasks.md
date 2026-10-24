# タスクリスト

[機能一覧](spec/features.md) の機能を漏れなく移植するための作業順。上から順に進める。
終わったら `[x]` にし、完了条件で使ったシナリオやテストの名前を書き添える。

現在の作業位置・直近の検証結果・調査の再現手順は [作業引き継ぎ](progress.md) を参照。

## 完了条件の決まり

- **正解照合**：`tools/FrTools/scenarios/` にシナリオを書き、`FrTools compare <シナリオ>` で `*.list.json`（プレビュー一覧）が一致すること。
- **画面比較**：2026-10-24 の UI 見直し（[ui-redesign.md](ui-redesign.md)）以降は参考扱い。`compare` の report.md 末尾の「合否」はテキストの記録（一覧・メニュー・ファイル結果）だけで決まる。横並び画像は、文字の欠け・重なり・見えない部品がないかを目で見るために使う。
- **単体**：Core にユニットテストがあり、`dotnet test` が通ること。

## 0. 基盤

- [x] T001 ツール（FrTools）：リソース抽出、作業コピー、フィクスチャ、シナリオ実行、画面・ツリー・一覧の比較
- [x] T002 リソース一覧（docs/spec/resources.md）と機能一覧（docs/spec/features.md）
- [x] T003 Preset.ini の読み込み（単体：PresetFileTests）
- [x] T004 正解データの保存：`FrTools golden <セット>` で `tools/FrTools/golden/*.json` のケースをオリジナル版に入力し、結果を `tests/ReflexRenamer.Core.Tests/Golden/` に保存する（GoldenRenameTests）
- [x] T005 ini の読み書き（UTF-16。キーの重複と順序を保つ）と `Flexible Renamer.ini` の設定モデル（A22）（AppSettings。ウィンドウ位置・ページ・フォルダ・リネームと属性の入力値・履歴を保存）
- [x] T006 Strings.ini による多言語化の基盤と `%%Key%%` の展開。キーと値の前後の空白は削る。既定は OS の言語（A23）（LanguageStringsTests、Golden: typical）
- [x] T007 画面の組み立て基盤：exe のダイアログテンプレートから WinForms のコントロールを作る（DialogBuilder、Name ＝ リソース ID）。メニューも MENU リソースから作る。アイコン・ビットマップを exe から抽出して埋め込む

## 1. リネーム処理（Core）— 画面より先に作る

- [x] T101 ワイルドカードのマッチとグループ割り当て（E01）（Golden: rename-basic, rename-edge, wildcard-dots）
- [x] T102 正規表現。boost と .NET の違い（`\<` `\>` `\Z` `\C` など）を変換で吸収する（E02）（Golden: rename-basic, rename-edge）
- [x] T103 置換文字列の展開：`\1`〜`\9`、大小文字変換 `\l\L\u\U\Q\E`、3段階処理（E04、E11）（Golden）
- [x] T104 ファイル関連のタグ `\0 \t \e \f \F \; \: \P`、プレビューでの `\`→`%`・`:`→`;` の置き換え（E05）（Golden: rename-basic, rename-options）
- [x] T105 連番 `?` と番号の種類（10進／16進（小文字）／アルファベット）、開始・ステップ、ランダム `*`（E06、B09）（Golden: rename-options）
- [x] T106 日時タグと strftime、`\#`、更新日時か作成日時か（E07、B11）（Golden: rename-basic, rename-options）
- [x] T107 文字変換（tr）と、全角・かな・漢数字・ローマ数字の範囲（E03）（Golden）
- [x] T108 既定の並び順は NTFS の列挙順（FileOrder）。見出しクリックでの並べ替えは T205（E10）
- [x] T109 定型処理のプレースホルダ展開と Param ビット（E08、B03）（TypicalTemplate。Golden: typical の 44 ケース。VBScript を使う 33、34、38〜41 は T116 で確認）
- [x] T110 フィルタ（`;` 区切り）、ファイル／フォルダの対象、再帰と深さ、並び順（FileCollector）（B01）（Golden: rename-options）。未確認：フィルタがフォルダにも効くか、保護された OS ファイルの除外（A28）
- [x] T111 グループごとの連番（A12、B18）（NumberGroupKey。グループ表示と連動）
- [x] T112 フォルダ振り分け（`..` と `/`）（B17）（RenameExecutor。RenameExecutorTests.SlashMovesIntoSubfolder。オリジナル版はこの環境では重複確認が出て照合できない）
- [x] T113 実行：リネーム、移動、コピー、ショートカット、ハードリンク、ごみ箱（B10、B13）（RenameExecutor と単体テスト。リネームと元に戻すは compare exec-rename、ごみ箱へ移動（名前が変わらない項目も処理、元に戻すは無効のまま、ボタンは「ごみ箱(R)」）は compare exec-recycle で一致。移動・ショートカット・ハードリンクは compare exec-tofolder、コピーは compare explore-destination-menu で実ファイルが一致（選択ダイアログは treeSelect で操作））
- [x] T114 重複時の処理と自動連番、エラー時の再試行・スキップ・中止（B14、B15）（重複確認ダイアログの初期状態・既定値、すべてスキップと連番を付けるの結果（exec-conflict-skip / exec-conflict-number）、エラーダイアログ（exec-error）が compare で一致）
- [x] T115 `Rename.log` と元に戻す（B16）（RenameLog。形式はオリジナル版のログに合わせた）
- [x] T116 スクリプト評価（VBScript／JScript）と StrFunc 相当の関数（E09、G02）（ActiveScript のホスト ScriptHost は完了。評価に失敗した項目は名前を変えない（explore-typical の digit：全角数字で VBScript がエラー）。評価順と StrFunc 相当の関数の実装・照合が未。2026-10-24：DLLの型情報を抽出し、32bit PowerShell＋登録不要COMで原本DLLの8項目を採取。GetTimeFormatも対象。調査スクリプト・結果・次の手順は docs/progress.md。2026-10-24（続き）：StrFunc互換を実装。原本DLLの378式（cases.txt）を採取し docs/research/strfunc-sample.json に保存、Core の StrFunc（LCMapString／StrConv／GetDateFormat／GetTimeFormat、PROPERCASE は原本独自の規則）・CFormat（sprintf の va_list 再現）・StrFuncObject（IDispatch）を追加。ScriptHost が CreateObject("StrFunc.Wrapper")／new ActiveXObject を差し替えて互換オブジェクトを返す。テスト：StrFuncTests.MatchesOriginalDllSamples（不定の12式を除き全件一致）、PresetScriptFunctionsUseStrFunc、JScriptCanUseStrFunc、RenameEngineScriptTests.TypicalScriptUsesPresetFunctions ほか。GoldenRenameTests は options.script／定型処理の Script 列でスクリプトを使うよう対応。残り：スクリプトを使う定型処理（33、34、38〜41）とスクリプトの正解データの採取・照合、タグ展開・スクリプト評価・連番展開の順序の確認）。2026-10-24（照合）：golden typical-script（定型処理 33・34・38〜41 の 10 ケース）と script-order（評価順・エラー・`*` の扱い）を採取して一致。連番はスクリプトより先に展開し、スクリプト使用時は `*` を残すように RenameEngine を修正。StrFunc を使う付属関数はオリジナル版では COM 登録が要るため StrFuncTests（DLL の実測）で照合）

## 2. メインウィンドウ

- [x] T201 メインフレーム、メニュー、アクセラレータ、ステータスバー（A01〜A03、A07）（配置は compare で一致。残る差は UI ツリー上の種類名）
- [x] T202 リバー：処理単位と機能のツールバー、フォルダ入力欄と履歴（A04〜A06）（入力欄の履歴はセッション内で開いたフォルダを新しい順に。compare explore-folder-history で照合）
- [x] T203 ドッキングペイン（フォルダ一覧、入力フォーム）と配置の保存・リセット（A08、A09、A21）（固定配置とリセットは完了。進捗：右端 5px のドラッグで幅変更（最小はフォルダ一覧 33px＝CSizingControlBar の既定、入力フォーム 271px＝オリジナル版は sizeVertCX=262 でも 271px で出すため、ファイル一覧は 33px 残す）、[BarState-*] の読み書き（Core `BarState`、オリジナル版と同じ Summary／Bar0〜6／SCBar 形式。左のドックバー 59420 の Bar# を並び順に使う）、起動時の復元、リセットで既定の幅と並びに戻す、つまみのドラッグ（反転枠を描き、列の中で放すと 2 ペインの左右を入れ替え）。幅変更・最小幅・保存（幅 265）・オリジナル版形式からの復元（幅 300）は画面で確認済み。フローティング：列の外で放す／つまみのダブルクリックでツールウィンドウ（`Ui/FloatingPaneWindow`、タイトルはペイン名、親はメインウィンドウ）に浮かせ、タイトルバーのダブルクリックで元の並びの位置に戻す。閉じるボタンはペインを隠す（リセットで戻る。ShowShellFolderBar も読み書き）。ini は MFC の SaveState と同じ形にした（浮かせたペインは BarID=59423・Floating=1・XPos/YPos がウィンドウ位置のドックバーのセクションに入れ、左のドックバーの Bar# から外す。ペインのセクションは Docking=1、MRUDockID=59420、MRUDock*Pos がドッキングに戻す位置、MRUFloatXPos/YPos、sizeFloatCX/CY。Visible=0 で隠した状態）。読むときは Docking=0＋XPos/YPos の形と、ID|0x10000 の場所取りも受け付ける。テスト：`BarStateTests`（配布物の ini から 126=396px・127=271px を読む、書式、往復、クランプ、`WritesFloatingPaneAsMiniFrameDockBar`、`RoundTripsFloatingAndHiddenPanes`、`FloatingPaneKeepsDockPositionWhenReordered`、`ReadsDockingZeroAsFloating`、`IgnoresPlaceholderIdsInDockBar`）。画面での確認が未：入れ替え、浮かせる・戻す・閉じる・リセット、浮かせたまま再起動。オリジナル版で浮かせたときの ini はまだ実測していない（Bar セクションの順番、浮かせたペインの XPos/YPos、Horz の値）。未実装：浮かせたウィンドウをドラッグして列に戻す、上下や同じ列へのドッキング、浮かせたウィンドウにフォーカスがあるときのメインメニューのショートカット、ドラッグ中の Esc での取り消し、sizeVertCY と ScreenCX/CY（VirtualScreen を使用）の値がオリジナル版と一致するかの確認）（2026-10-24（画面）：右端のドラッグで幅が変わり、最小幅まで縮むことを compare explore-dock でオリジナル版と並べて確認。BarState の保存（debug-dock-save で幅 265 が sizeVertCX に入る）と、オリジナル版形式の BarState からの復元（compare explore-dock-restore で幅 300 が両方同じ位置）を確認。残り：フローティング、上下へのドッキング、同じ列への積み重ね）（2026-10-24（フローティング）：つまみのダブルクリックで浮かせ、タイトルバーのダブルクリックで戻すことを compare explore-dock-float で確認。浮いた窓の閉じるボタンはオリジナル版にないので外した。未実装：浮いた窓をドラッグして列へ戻す操作、上下や同じ列へのドッキング）
- [x] T204 フォルダツリー（シェル名前空間、F5 更新）（A08）（ShellFolderTree。エクスプローラーのナビゲーション列挙と同じ並び）
- [x] T205 ファイル一覧：ページごとの列、アイコン、並べ替え、見出しメニュー、グループ表示、シャッフル（A10〜A12）（列の幅は ini の ListCtrl-1000-0-0 を読み書きし比率で配分。並べ替え（名前・サイズ・更新日時・種類、安定、サイズは降順固定）とグループ表示（見出し行なし、グループはキーの昇順）は compare explore-sort で一致。シャッフルは手動確認（T803））
- [x] T206 一覧の操作：削除、全選択、選択の切り替え、ダブルクリック、シェルのコンテキストメニュー（A13）（Shell.Application の Verbs で表示）
- [x] T207 新しい名前の直接編集（A14）（F2、列のクリック、↑↓で連続編集。検索・置換を変えると消える）
- [x] T208 ファイル指定モードとドラッグ＆ドロップ、コマンドライン引数（A15、A16）（exe アイコンへのドロップは引数と同じ扱い）
- [x] T209 フォルダの監視（A19）（FileSystemWatcher、0.7秒後に作り直し）
- [x] T210 表示設定とフォント（A20）（FontDialog。設定の Font/FontFace に保存）
- [x] T211 お気に入り（A18）（設定の [Favorites] MRU。整理は DIALOG 139 を流用）
- [x] T212 バージョン情報、ヘルプ、ホームページ、言語の切り替え（A23、A24）
- [x] T213 オプションダイアログとシェル連携（A25、A17）（ツリー形式に作り直し、compare explore-options で画面ツリーが一致。「送る」は3項目で起動引数 /Rename などを渡す。重複時の既定値のキーは作者の ini に合わせて DuplicateIndex 等。DuplicateIndex=2 の意味は未確認）
- [x] T214 進行状況ダイアログとタスクバーの進行状況（A26）（ProgressDialog と TaskbarProgress（ITaskbarList3））
- [x] T215 ツールチップ（A27）（Ui/Tooltips。文字列テーブルの文を意味から各コントロールに対応付けた）

## 3. リネームページ

- [x] T301 対象・フィルタ・再帰の欄（B01）（フィルタは区切りごとに列挙し重複も残す、`.` のない語は拡張子扱い、再帰なしではフォルダにも適用、既定の履歴5件。compare explore-target / explore-target-filter で一覧と履歴が一致。FileCollectorTests.FilterIsListedPerPatternAndAppliesToFolders、RecursiveListsAllFoldersAndFilesPerPattern）
- [x] T302 定型メニューのツリーとパラメータ欄（B02、B03）（動作は compare explore-typical で一致。カテゴリのアイコンはビットマップ 267 から）
- [x] T303 高度なリネームの欄、方法ごとの設定と履歴、ワンポイント表示（B04、B05）（「高度なリネーム (選択されたメニュー項目)」のクリックで展開した検索・置換を持って切り替える。compare explore-typical-advanced。スクリプトの有効／無効／再有効での説明文は compare explore-script の script-on/off/restored で原本と一致）
- [x] T304 入力サポートメニュー（B06）と正規表現オプション（B07）（Win32 のポップアップ。compare explore-rename-menus で全メニュー一致）
- [x] T305 プリセットのメニューと整理ダイアログ（B08）（PresetStore：設定ファイルの [Preset]、なければ Preset.ini から取り込み）
- [x] T306 連番欄と番号の種類ダイアログ（B09）（NumberingDialog、連番欄右のメニュー）
- [x] T307 実行する動作、送り先、オプションメニュー（B10、B11）（実行ボタンを分割ボタンにし、▼ で MENU 170。送り先の選択を確認より先にし、SHBrowseForFolder 形式に。履歴は DestinationHistory（AppSettingsTests.DestinationHistoryIsStoredOldestFirstAndListedNewestFirst）。compare explore-destination-menu でメニュー（空・1件）、選択ダイアログ、確認、コピー結果が一致。サブフォルダ作成はオリジナル版でも常に無効）
- [x] T308 プレビューの即時更新（B12）（compare rename-wildcard で一致）
- [x] T309 実行・重複確認ダイアログ・エラーダイアログ・元に戻す（B13〜B16）（確認メッセージ、重複確認ダイアログ、エラーダイアログ（exec-error：長すぎる名前）、元に戻すの確認・完了メッセージが compare で一致）

## 4. タグ・リネーム

- [x] T401 タグの読み取り：ID3v1/v2（C06）（Id3Reader と単体テスト、golden tagrename（music-*）との照合で一致。v2 があれば v1 を読まない、ジャンル表と `(Other)`、v1 なしの TrackNumber `-1` を反映。日本語タグはオリジナル版が文字化けするので比べない）
- [x] T402 WMA、APE、MPC、OGG（C07）（AsfReader、ApeReader、OggReader を実装。OGG は単体テスト済み、WMA/APE は実ファイルでの確認が未）（2026-10-24：合成データの単体テスト TagReaderFormatTests（ASF・APEv2）を追加）（2026-10-24（照合）：フィクスチャ tags2 の合成 WMA・APE・MPC をオリジナル版に読ませ、golden tagrename-formats で一致。WM/TrackNumber は DWORD のときだけ使い文字列なら -1、WM/Track は +1、APE のジャンルなしは (Other)・曲番号なしは 00、ID3v1.* も同じ値、に修正。OGG は実際のファイルと同じページ構成（2 ページ目にコメントとセットアップヘッダ、音声ページ付き）ならオリジナル版が読み、一致。ID3v1.* も同じ値）
- [x] T403 EXIF と値の対応表（C08）（ExifReader と TagFormatter の対応表、golden tagrename（picture-*）との照合で一致。ISO 速度は `ISO-400`。撮影日時はオリジナル版が「Invalid DateTime」になるので比べない）
- [x] T404 IPTC（C09）（IptcReader を実装。実ファイルでの確認が未）（2026-10-24：合成データの単体テスト TagReaderFormatTests（JPEG APP13・PSD、UTF-8 指定）を追加。Keywords はオリジナル版の TagDef.ini にないので扱わない）（2026-10-24（照合）：オリジナル版はデコードできる JPEG からしか IPTC を読まない。System.Drawing で作った JPEG に差し込んだ iptc5.jpg で全14項目が golden tagrename-formats で一致。DateCreated は日時にせず文字列のまま、に修正。PSD は合成ファイルを読まないので未照合）
- [x] T405 Office・PDF（C10）、HTML（C11）（OLE 複合文書、docx 系、PDF、HTML を実装。HTML は golden tagrename（web-*）で照合。OLE は実ファイルでの確認が未。合成 PDF はオリジナル版が読めないので比べない）（2026-10-24（照合）：Windows の構造化ストレージで作った本物の OLE 複合文書（doc・xls、ANSI と Unicode のプロパティセット）と、xref の正しい PDF をフィクスチャ docs に入れ、golden tagrename-docs で一致。DOC.Company はオリジナル版が OLE から読まないので読まないように修正。PDF は xref がない合成ファイルだとオリジナル版が読まない）
- [x] T406 Windows のプロパティシステムとプロパティ定義ダイアログ（C03）（PropertyTagDefinitions、ShellColumns、TagRenamePage のプロパティモード、PropertyDefinitionDialog。compare explore-tagproperty で一覧・タグ一覧・ダイアログの列の並びと既定のチェックが一致。PropertyTagDefinitionsTests）
- [x] T407 ページの画面：タグの種類、ファイルの種類、書式、タグ一覧メニュー、プリセットメニュー（C01、C02、C04）（compare explore-tagrename で一覧・全メニュー・設定の候補が一致。残る UI ツリーの差は WinForms の子要素の名前だけ。縦方向の配分は TagRenamePage.OnLayout）
- [x] T408 タグ・リネームの設定ダイアログ（C05）（TagRenameSettingsDialog。既定値・書式の候補・画面ツリーが compare explore-tagrename の settings で一致）
- [x] T409 タグを一覧の列に表示（C12）（見出しメニューの後ろにタグ項目。compare explore-tagcolumns / debug-tagcolumn-select で一覧とメニューが一致）

## 5. 属性

- [x] T501 タイムスタンプの5つの方式と属性の変更（Core）（D02〜D05）（AttributeChanger と単体テスト。連続生成は「前に／後に」を表示して向きに従い、下限（開始）の既定を前日 0:00 に修正。compare exec-attribute-seq で前に2日・後に1日の結果が一致。AttributeChangerTests.SequentialEarlierGoesBackward。撮影日を元にした揃えるは、合成 JPEG の撮影日時をオリジナル版が読めない（タグ・リネームの Invalid DateTime と同じ）ので比べられない。オリジナル版はその場合に日時を変えない）
- [x] T502 ページの画面（D01〜D05）（AttributePage。一覧の規則と方式ごとの欄は compare explore-attribute で一致。揃える・シフト・属性変更の実行結果（タイムスタンプ・属性）は compare exec-attribute で一致。残る UI ツリーの差は WinForms の DateTimePicker の見え方）

## 6. 連番オブジェクト

- [x] T601 生成処理とプレビュー（Core）（F01〜F03）（NumberedObjectGenerator と単体テスト。名前に `?` がないときの一覧は compare explore-numbered で一致）
- [x] T602 ページの画面（F01〜F03）（NumberedObjectPage。プレビュー一覧は compare explore-numbered、生成結果（空ファイル 1K、空フォルダ）は compare exec-numbered で一致）

## 7. スクリプト

- [x] T701 スクリプトメニュー、関数一覧、スクリプトの編集（G01）（compare explore-script / explore-script-functions で付属関数・両言語の埋め込み関数・言語チェック・プレビューが一致。script-insertion で挿入位置と括弧内の選択、script-language-settings で保存言語の復元が一致。関数列挙は ScriptHostTests。編集先は Script.vbs / Script.js に修正。外部エディタの画面操作は T803）
- [x] T702 ユーザースクリプトの作成・読み込み・保存後の再読み込み（G03）（ScriptWorkspaceTests：UTF-16の新規作成、既存内容の保持、付属関数との併用、保存後の関数更新、構文エラー修正後の復旧。compare explore-script-user でVBScript/JScriptのメニューとプレビューが一致）

## 8. 仕上げ

- [x] T801 全シナリオで `compare` を実行し、残った差分を洗い出す（2026-10-24：13 シナリオを実行。一覧・メニュー・ファイルの結果はすべて一致（差があったのは拡張子なしファイルの「種類」とスクリプトエラー時の名前で、修正済み）。残る UI ツリーの差は WinForms の子要素の名前・種類（ComboBox の Edit、DateTimePicker、ListBox 項目名、スピンボタン）とフォルダのアクセス日時）（2026-10-24 夕方：compare all を 55 シナリオ中 39 件まで実行（約 42 分、1 件約 1 分）して中断。残りは lang-* と rename-wildcard・script-*（後者は当日個別に一致を確認済み）。一覧・ファイルの差は既知のもの（フォルダのアクセス日時、合成 JPEG の撮影日時、合成 PDF）だけ。exec-error と exec-subfolder はオリジナル版側の自動操作の失敗（キャプチャの 0x80040201、重複確認でフィルタ欄が戻らない）で比較できず。debug-tagkind は古い観察用シナリオで移植版の一覧を見つけられない）
- [x] T802 9言語すべてで画面が崩れないことを確認する（lang-*.json：言語を設定して各ページを撮影。例外なし。独語などの長い訳語は Win32 と同じく1行で切る（BS_MULTILINE のないボタン・チェックボックスは折り返さない）ように直した。画像は .work/out/lang-*/clone/。細部の目視は T803）
- [ ] T803 手動確認項目（シェル連携、ドロップ、監視など）を人が確認する（追加：見出しメニューの「グループで表示」（フォルダ・日付・種類・拡張子）とシャッフルの見た目、フォルダ振り分け `newdir/ ` の実行、スクリプトの編集で開く外部エディタと編集後の反映）（2026-10-24 追加：ペインの幅を変えて終了→再起動で戻ること、浮かせたペインの移動・大きさ変更と再起動後の復元、実行ボタンの ▼ の見た目）（2026-10-24 自動で確認済み：フォルダの監視・シャッフル・拡張子でのグループ表示（debug-watch-shuffle）、`newdir/` の振り分け（exec-subfolder の移植版）、浮かせたペインの保存と再起動後の復元（debug-float-save / debug-float-restore）、幅の保存と復元（debug-dock-save / explore-dock-restore）、実行ボタンの ▼ の見た目。残り：シェル連携（レジストリ等を書き換える）、エクスプローラーからのドロップ、外部エディタでの編集。どれも利用者の環境を操作するので許可を得てから）（2026-10-24：エクスプローラーからのドロップはユーザーが確認済み。シェル連携と外部エディタでの編集は保留）

## 9. UI 見直し（Reflex Renamer）

方針と決定は [ui-redesign.md](ui-redesign.md)。見た目はオリジナル版に合わせず、Windows 11 のアプリとして自然な形にする。

- [x] T901 不具合の修正：重複確認ダイアログの「処理」の中身が GroupBox に隠れる（DialogBuilder の Z オーダー）、ステータスバーの件数が切れる、一覧の右 4 列が 32px で読めない、オプションの見出しアイコン（存在しない ID 128）、エラーダイアログが例外の内容を捨てる、フォルダ欄の先頭文字が隠れる、連番ページの横スクロール、名前欄のプレースホルダー、定型メニューの太字と切れ（2026-10-24：run explore-pages / exec-rename の画面で確認。列幅は狭いときにフォルダ→種類→日時の順に縮めて横スクロールを避ける）
- [x] T902 土台：既定フォントをシステムフォント（Yu Gothic UI）にし、DLU の基本値をフォントの実測から求める。PerMonitorV2。メニュー・ステータスバーを自動サイズに。アクセスキー下線の強制表示をやめる。フォルダツリーとファイル一覧とページ内の一覧に Explorer テーマ（2026-10-24：Tahoma 9pt では今までどおり 7/14。100% 表示で文字の欠けなし。高 DPI は未検証）
- [x] T903 名称と版：Reflex Renamer 0.1.0。exe は ReflexRenamer.exe、設定は Reflex Renamer.ini、インストーラーは ReflexRenamer-Setup-<版>.exe。バージョン情報は「Based on Flexible Renamer 8.4 (C) 2000-2011 Naru」。シェル登録名も Reflex Renamer（2026-10-24。FrTools は `Sandbox.TitleFor` で `window:Flexible Renamer` を読み替える）
- [x] T904 ダークモード：`Application.SetColorMode(SystemColorMode.System)`。追従しない部品を修正（くぼみ線、無効なテキストボックス、ページ内の一覧、ラジオボタンの文字、ツールバー）（2026-10-24：OS がダークの環境で 4 ページと重複確認を目視。ライトモードは第 2 便までの画面で確認。残り：スピンボタン（msctls_updown32）が明るいまま）
- [x] T905 ページ切り替えをタブに（`PageTabStrip`。各タブは AccessibleRole.CheckButton で AccessibleName は従来どおり）。ツールバーを 1 段に（ファイル指定／フォルダ指定 + フォルダ欄）。アイコンは Segoe Fluent Icons のグリフ（`FluentGlyphs`）。フラットな描画（`FlatToolStripRenderer`）（2026-10-24：explore-pages のクリックがそのまま通る）
- [x] T906 ドッキング機能の廃止：ペインの並べ替え・浮かせる機能を削除（FloatingPaneWindow を削除、DockPane は右端のドラッグで幅を変えるだけ、ペインは重ねずに並べる）。BarState は幅と表示だけ使う。シナリオ explore-dock*・debug-dock-save・debug-float-* を削除（2026-10-24）
- [x] T907 compare の合否をテキストの記録だけにする（Compare.Run の「合否」行）。AGENTS.md と tasks.md の完了条件を更新（2026-10-24）
- [ ] T908 残り：高 DPI（125%・150%）での確認、ライトモードでの第 3 便以降の画面確認、スピンボタンのダークモード、ページ見出しの 32px アイコン（テンプレートの SS_ICON）の扱い、lang-* シナリオで 9 言語の画面を撮り直す
