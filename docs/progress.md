# 作業引き継ぎ

更新日：2026-10-24。タスク全体の状態は [tasks.md](tasks.md)、仕様は [spec/features.md](spec/features.md)。この文書は直近の作業を再開するための記録であり、過去セッションの逐語ログではない。

## 現在地

- 2026-10-24 夜：UI 見直し（tasks.md の 9 章、方針は ui-redesign.md）。製品名を Reflex Renamer 0.1.0 に改名。システムフォント・PerMonitorV2・ダークモード・タブ化・1 段ツールバー・ドッキング廃止・列幅と各ダイアログの不具合修正を実施。実装は agy（gemini-4-argon-high）に 3 便に分けて委譲し（ファイル編集のみ）、ビルド・テスト・撮影・FrTools 側の変更（exe 名、ini 名、`window:` の題名の読み替え、合否基準）は Claude。残りは T908。compare の合否はテキストの記録だけで決まり、画像・UI ツリーの差は参考。

- 2026-10-24 夕方の作業で T113、T116、T203、T301、T307、T402、T404、T405、T501、T801 を完了。未チェックは T803（人による手動確認）だけ。T203 は浮いた窓のドラッグでの再ドッキングと上下へのドッキングが未実装。T801 の残る差は tasks.md の T801 行。
- この回に入れた主な変更（詳細と照合に使ったシナリオ・正解データは tasks.md の各行）：
  - フィルタ（FileCollector）：区切りごとに列挙して重複も残す、`.` のない語は拡張子扱い、再帰なしではフォルダにも適用、既定の履歴5件。
  - 別フォルダへの実行：実行ボタンを分割ボタン（BS_SPLITBUTTON）にして ▼ で最近送ったフォルダ（MENU 170、DestinationHistory）。送り先の選択（SHBrowseForFolder 形式）を確認メッセージより先に。
  - スクリプト：StrFunc 互換（Core/Scripting の StrFunc・CFormat・StrFuncObject）。連番はスクリプトより先に展開し、スクリプト使用時は `*` を残す。
  - 属性：連続生成も「前に／後に」に従い、下限（開始）の既定は前日 0:00。
  - タグ：WMA・APE・OGG・IPTC・OLE・PDF をオリジナル版に読ませて照合し、読み取りの細部を修正（tasks.md の T402・T404・T405）。
  - ペイン：右端のドラッグで幅変更、BarState の保存・復元、つまみのドラッグで左右入れ替え（Core/Settings/BarState）。
- FrTools に追加：`drag` ステップ、フィクスチャ `tags2`（WMA・APE・MPC・OGG・IPTC）と `docs`（OleFixture による本物の OLE 複合文書、xref のある PDF）、正解データ `typical-script`・`script-order`・`tagrename-formats`・`tagrename-docs`。
- オリジナル版に読ませる合成ファイルの条件（実測）：IPTC はデコードできる JPEG（System.Drawing で作る）、OGG は実際と同じページ構成、PDF は xref 付き。最小構成だと何も読まない。
- 完了チェックにも限定事項がある。T110のフォルダへのフィルタ・保護されたOSファイル、T112のフォルダ振り分けの原本実行照合、T213のDuplicateIndex=2、T404 の PSD、T501 の撮影日（合成 JPEG の撮影日時をオリジナル版が読めない）などは未確認。

## 直近に実装したもの

- `ScriptHost.FunctionNames()`：ActiveScriptの型情報から関数を宣言順に列挙。付属VBScriptの19関数と両言語のユーザー関数をテスト。
- `ScriptBuiltins` と `Scripting/*.Builtins.txt`：原本のメニューから採取した両言語の埋め込み関数一覧。
- `ScriptWorkspace`：付属の `Script.Preset.*` に続けてユーザーの `Script.vbs`／`Script.js` を読み込む。新規ファイルはBOM付きUTF-16。既存内容を保持し、保存した内容が変わればエンジンを作り直す。
- `RenamePageBase`：言語選択・設定保存、関数挿入、選択言語のユーザーファイルをメモ帳で開く処理。画面に戻ったときにプレビューを更新。
- `ComboEditSelection`：子EditにUI Automationで直接フォーカスした場合にも、フォーカス喪失前の選択範囲を保持する。関数挿入後は括弧内を選択。
- `RenamePage`／`TagRenamePage`：選択したスクリプト言語で評価。RenamePageのプリセットにも言語を引き継ぐ。
- `FrTools`：`NativePopup`で子メニューを読み取り・選択。`ScriptFixture`でユーザースクリプトを比較中だけ差し替え、終了時に元の内容を復元。`readValue`で入力値を記録。

前段では、作業コピーのルートを残して中身だけ再作成する修正、終了するプロセスをexeのフルパスで絞る修正、スクリプト有効／無効時の説明文切り替えも実施済み。

## 検証済みの範囲

- 直近の全体ビルド：警告0、エラー0。
- 直近の全単体テスト：261件成功、失敗0、スキップ0。追加分は `ScriptHostTests` と `ScriptWorkspaceTests`。選択範囲のUI修正後、初回コミット前にも全体ビルドと全単体テストを再実行して成功した。

| シナリオ | 確認結果 |
|---|---|
| `explore-script` | 関数メニュー、置換サポート、プレビューが一致。スクリプトのオン／オフ／再オン時の説明文も一致 |
| `explore-script-functions` | VBScript/JScriptの埋め込み関数、言語チェック、JScriptのプレビューが一致。挿入値は `seedDigitNumber()`、`toFullKana()`、`UCase(string)` が一致 |
| `explore-script-user` | 両言語のユーザー関数一覧・プレビューが一致。`UserName()` の挿入値も一致 |
| `script-language-settings` | 保存したJScriptの復元、関数メニュー、プレビューが一致 |
| `script-insertion` | 挿入後にxを入力した結果が `seedDigitNumber(x)`、`UCase(x)`、`Left(x)` で一致 |

結果は `.work/out/<シナリオ>/compare/report.md`。入力値だけを比較した `script-insertion` と設定復元シナリオは全出力一致。その他のUIツリーにはWinFormsのComboBox/Editの名前やAutomationIdなどの差が残る。画面全体の画素差も約14～15%あり、全画面の完全一致を達成したわけではない。T801で継続確認する。

外部エディタの画面上での編集・保存・復帰の一連操作は未確認（T803）。ユーザーファイルの作成・保持・再読み込み・構文エラーからの復旧は単体テスト済み。T701/T702の完了はこの検証範囲による。

## T116：StrFuncの調査結果と互換実装

参照DLLは `FlexRenamer/StrFunc/StrFunc.dll`（32bit COM）。`FlexRenamer/` 内で実行せず、`.work/strfunc/StrFunc.dll` にコピーして調べた。

- クラス：`StrFunc.Wrapper`、CLSID `{4FCD6060-957F-476A-B8DA-A4A44D06E594}`。
- インターフェース：`IWrapper`、IID `{A7B94F24-0D2F-49C8-8412-4E98C5D850D3}`。
- 型ライブラリ：`{088E76DC-EEFF-48DB-B193-2FE1A10A3A2F}`、バージョン1.0。
- 公開関数：`LCMapString(LocaleID, MapFlags, SrcStr)`、`StrConv(SrcStr, Conversion[, LocaleID])`、`sprintf(Format, Args...)`、`vsprintf(Format, Args)`、`GetDateFormat(LocaleID, Flags, Date, Format)`、`GetTimeFormat(LocaleID, Flags, Date, Format)` と定数41個。
- `DllGetClassObject` だけで生成すると、呼び出し時に `TYPE_E_LIBNOTREGISTERED (0x8002801D)` になった。COMクラスと型ライブラリを宣言したマニフェストで `CreateActCtx`／`ActivateActCtx` を行うと呼び出せる。システムへのCOM登録は行っていない。

型情報は [strfunc-typeinfo.txt](research/strfunc-typeinfo.txt)、実測値は [strfunc-sample.json](research/strfunc-sample.json)（378式）。式の一覧は `tools/FrTools/strfunc-reference/cases.txt`（1行1式の VBScript。`f` が原本のオブジェクト）。`Probe.ps1` が 32bit の ScriptControl で各式を評価し、値・`TypeName`・エラー番号と説明を記録する。日本語Windowsでの値。

### 実測で分かった挙動

- 引数の数：`StrConv` は2〜3個（LocaleID 省略可、0 扱い）、`sprintf` は1個以上、ほかは固定。違うとエラー450。`Null` を文字列引数に渡すとエラー13、`Empty` は空文字、数値は文字列に変換される。定数に引数を付けるとエラー451、ない名前は438。
- `LCMapString`：入力は最初の NUL 文字まで。フラグが不正だと空文字（エラーにならない）。`LCMAP_SORTKEY` はバイト列を UTF-16 の文字として返す。
- `LCMAP_PROPERCASE`（`StrConv` の 3 も）は Win32 に任せていない。Win32 は `_` `-` `.` の後も大文字にするが、原本は他のフラグ＋小文字化の後、先頭と空白（旧CRTの iswspace：U+0009〜000D、U+0020、U+00A0、U+0100 以上は C1_SPACE。U+0085 は含まない）の直後の1文字だけを大文字にする。小文字化は LocaleID と言語規則を使い、大文字化は言語規則を使わない（トルコ語でも i → I）。
- `StrConv`：Conversion の各ビットを LCMAP フラグに置き換えて `LCMapString` と同じ処理。知らないビットは無視。`vbNone`（0）と `vbLinguisticCasing` 単独は空文字。
- `sprintf`／`vsprintf`：VC++ 2008 世代の CRT（指数3桁 `e+004`、`1.#INF00`、`-1.#IND00`、`%.0f` は四捨五入、17桁で丸め）。引数は 32bit の va_list に積む：Integer・Long・Boolean は4バイト、Single はビット列のまま4バイト、Double は8バイト、文字列はポインター。Byte・Currency・Date・Null・Empty は何も積まない。配列は入れ子も展開。足りない引数は 0。結果は NUL で切れる。型が合わないと隣の引数とつながった値になる（`%d` に 3.5 で 0 と上位ワード）。
- `GetDateFormat`／`GetTimeFormat`：Format は省略不可で、空文字だと結果も空（既定の書式にはならない）。日付引数は VT_DATE と Double だけを読み、それ以外（文字列・整数・Null）は前回の値などが残る。**変数（参照渡し）の日付は正しく読まない**。
- 原本のプロセスが落ちる式：`%n`、`%Z`、`%s` に数値・Boolean・参照渡しの文字列。cases.txt ではコメントにしてある。

### 互換実装（Core/Scripting）

- `StrFunc`：定数と各関数。Win32 の `LCMapStringW`／`GetDateFormatW`／`GetTimeFormatW`／`VariantTimeToSystemTime` を 64bit から直接呼ぶ。PROPERCASE は上記の原本の規則で実装。
- `CFormat`：`sprintf` の書式化。va_list の積み方、旧 CRT の丸め・特殊値表記・`%a`・`%S`（UTF-16 を ANSI として読む）まで再現。
- `StrFuncObject`：`ComWrappers` で作る IDispatch。引数は `VariantChangeType` で変換し、原本と同じ HRESULT（450/13/438/451 になる）を返す。
- `ScriptHost`：名前付き項目 `ReflexRenamerStrFuncWrapper` として渡し、前置きのスクリプトで VBScript の `CreateObject` を差し替える（`StrFunc.Wrapper` 以外は `GetObject("", ProgID)` で作る）。JScript は `ActiveXObject` を無名関数で包み直す。前置きの `CreateObject` は `FunctionNames()` に出さない。`GetItemInfo` は NULL の出力先を受け取るのでポインターで宣言した。
- 原本との意図的な違い：参照渡しの数値・日付も値として読む。日付引数に日付以外を渡したら日付に変換する（原本は不定）。

テスト：`StrFuncTests.MatchesOriginalDllSamples`（結果が不定の12式を除く366式が型・値・エラーとも一致）、`PresetScriptFunctionsUseStrFunc`（Script.Preset.vbs の toUpperCase など17式）、`PresetStrConvPassesVariablesByReference`、`OtherProgIdsStillCreateObjects`、`JScriptCanUseStrFunc`、`RenameEngineScriptTests.TypicalScriptUsesPresetFunctions`／`ScriptCanCallStrFuncThroughPresetScript`。`GoldenRenameTests` は、ケースの `options.script`（言語名）か定型処理の Script 列があればスクリプトを有効にし、配布物の Script.Preset.* を読み込む（正解データ側のステップでスクリプトのチェックと言語を合わせる）。

### 再現手順（PowerShell、リポジトリ直下）

```powershell
New-Item -ItemType Directory -Force .work/strfunc | Out-Null
Copy-Item -LiteralPath FlexRenamer/StrFunc/StrFunc.dll -Destination .work/strfunc/StrFunc.dll
Copy-Item -LiteralPath tools/FrTools/strfunc-reference/StrFunc.manifest -Destination .work/strfunc/StrFunc.manifest
powershell.exe -NoProfile -File tools/FrTools/strfunc-reference/Inspect.ps1 -ReferenceDirectory .work/strfunc
& "$env:WINDIR/SysWOW64/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -ExecutionPolicy Bypass -File tools/FrTools/strfunc-reference/Probe.ps1 -ReferenceDirectory .work/strfunc
Copy-Item .work/strfunc/sample.json docs/research/strfunc-sample.json
```

`Probe.ps1` は32bit Windows PowerShell で実行する（ScriptControl `msscript.ocx` は32bitだけ）。BOM 付き UTF-8 で保存する（5.1 は BOM なしを ANSI として読む）。原本が落ちると終了コードが0以外になり、標準エラーの最後の行が原因の式。採り直すと、不定の式（アドレスや前回の時刻を読むもの）だけ値が変わる。

### 残っていること

- 照合済み（golden typical-script / script-order、StrFuncTests）。StrFunc を使う付属関数はオリジナル版では COM 登録が要り、この環境では評価エラーになるため、プレビューでは照合できない。
- 細かい違い：`TypeName(f)` が原本の "IWrapper" ではなく "Object"、JScript の配列を `vsprintf` に渡しても展開しない。

## 保存状態と注意

- Gitは `master`。2026-10-24にユーザーの依頼で、ソース・文書・テスト・ツールを初回コミットとして保存。続けてamendし、参照用配布物 `FlexRenamer/` はローカルに保持してGit管理から除外した。配置手順は [reference-setup.md](reference-setup.md)、22ファイルのサイズ・SHA-256は `reference-files.json`。最新のコミットは `git log -1`、その後の変更は `git status` で確認する。リモートは未登録で、pushは行っていない。ローカルGitへの記録と外部バックアップは別。
- `.work/` はgitignore対象。直近のStrFunc調査は、再現用スクリプトを `tools/FrTools/strfunc-reference/`、結果を `docs/research/` に保存し直したので、`.work/` を消しても再開手順は残る。
- 前担当はClaude。過去セッションのJSONLは参照専用であり、変更・削除・再開しない。現在のワークスペースを正として作業する。
- `FlexRenamer/` の配布物は変更しない。原本アプリは `.work/sandbox/original/` から実行する。
