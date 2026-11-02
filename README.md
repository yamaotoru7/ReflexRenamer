# Reflex Renamer

ワイルドカード・正規表現・タグ情報・スクリプトを使える、Windows 用のファイルとフォルダの一括リネームツールです。
入力に合わせて新しい名前をすぐに一覧で確かめられ、実行したあとでも元に戻せます。

Naru 氏の [Flexible Renamer](https://www.vector.co.jp/soft/winnt/util/se131133.html) を参考に、いまの Windows（10 / 11、64 ビット）向けに作り直したものです。

[English](#english)

## できること

- **リネーム**：文字列や連番の追加、文字の削除、大文字・小文字や全角・半角の変換、置換。定型メニューから選ぶだけでも、ワイルドカード・正規表現（Perl 互換）・文字変換で細かく指定することもできます。
- **タグ・リネーム**：音楽ファイルの ID3 タグ、写真の EXIF、Office 文書や PDF のプロパティなど、ファイルの中の情報を名前に使います。
- **属性**：タイムスタンプ（作成・更新・アクセス日時）と属性（読み取り専用、隠しなど）をまとめて変えます。
- **連番オブジェクト**：連番の付いた空のファイルやフォルダ、ファイルのコピーを一度に作ります。
- サブフォルダ以下もまとめて処理する、別のフォルダへ移動・コピーしながら名前を付ける、日付やタグごとにフォルダを作って振り分ける、といったこともできます。
- VBScript / JScript で自分の関数を書いて、置換に使えます。
- 画面は日本語・英語・ドイツ語・スペイン語・フランス語・イタリア語・ポルトガル語（ブラジル）・中国語（簡体字・繁体字）で表示できます。ライト／ダークの配色にも対応しています。

## インストール

1. [Releases](https://github.com/yamaotoru7/ReflexRenamer/releases) から `ReflexRenamer-Setup-<版>.exe` をダウンロードして実行します。
2. 管理者権限は要りません。`%LocalAppData%\Programs\Reflex Renamer` に入ります。.NET は同梱しているので、ほかにインストールするものはありません。

インストーラーにはコード署名をしていないため、初回は「Windows によって PC が保護されました」と表示されることがあります。その場合は「詳細情報」→「実行」で進めてください。

設定（`Reflex Renamer.ini`）と、元に戻すための記録（`Rename.log`）はインストール先のフォルダに作られます。上書きインストールやアンインストールでは消えません。

## 使い方

アプリの **ヘルプ** メニューから、詳しい説明（日本語・英語）をブラウザーで開けます。
ヘルプの原稿は [src/ReflexRenamer/Help/](src/ReflexRenamer/Help/) にあります。

## ソースからビルドする

必要なもの：Windows、[.NET 10 SDK](https://dotnet.microsoft.com/download)

```sh
dotnet build                               # ビルド
dotnet run --project src/ReflexRenamer     # 起動
```

インストーラーを作るには、[Inno Setup 6](https://jrsoftware.org/isinfo.php) を入れてから次を実行します。出力は `.work/installer/` です。

```sh
winget install --id JRSoftware.InnoSetup -e --scope user
pwsh tools/build-installer.ps1
```

テストと、オリジナル版との比較ツールには、Flexible Renamer の配布物が別に必要です（リポジトリには含めていません）。
置き方は [docs/reference-setup.md](docs/reference-setup.md)、開発の進め方は [AGENTS.md](AGENTS.md) を見てください。

## 謝辞

Reflex Renamer は、Naru 氏が作られた **Flexible Renamer** を参考にして作りました。

Flexible Renamer は、検索と置換の結果をその場で一覧に映すわかりやすさと、定型メニューから正規表現・タグ・スクリプトまでそろえた奥深さを両立した一括リネームツールです。
画面の構成や操作、置換の書式、プリセットなど、このアプリのほとんどの部分は Flexible Renamer（v8.4）の設計にならっています。
すぐれたソフトウェアをフリーソフトウェアとして公開してくださった Naru 氏に、心から感謝します。

- 言語ファイル（`*.Strings.ini`）、プリセット（`Preset.ini`）、タグ定義（`TagDef.ini`）、スクリプトのプリセットは、Flexible Renamer の配布物から取り込み、製品名などを直して使っています。
- 言語ファイルの翻訳は、Flexible Renamer の翻訳者の方々によるものです。ありがとうございます。
  - ドイツ語：Stefan
  - スペイン語：Elena Lozano & F. C. A.
  - フランス語：Pascal FLEUTOT
  - イタリア語：Emiliano Bina
  - ポルトガル語（ブラジル）：Luís Henrique
  - 中国語（簡体字）：SamWong
  - 中国語（繁体字）：rexx
  - 日本語・英語：Naru
- Reflex Renamer は Flexible Renamer の公式版ではなく、Naru 氏とは関係のない個人のプロジェクトです。Reflex Renamer についてのお問い合わせを、Naru 氏に送らないようお願いします。

## ライセンス

[MIT License](LICENSE) です。

ただし、上の謝辞に書いた Flexible Renamer から取り込んだデータファイル（[src/ReflexRenamer/Data/](src/ReflexRenamer/Data/) の言語ファイル・プリセット・タグ定義・スクリプトのプリセット）と、オリジナル版から取り出したアイコンとビットマップ（`src/ReflexRenamer/Assets/` のうち `app.ico` 以外）、ダイアログとメニューの定義（`docs/spec/resources.*`）は、MIT License の対象外です。著作権はそれぞれの作者にあり、Flexible Renamer の転載条件（自由）にもとづいて同梱しています。

---

## English

Reflex Renamer is a batch file and folder renaming tool for Windows 10 / 11 (64-bit), with wildcards, regular expressions, file tags (ID3, EXIF, Office/PDF properties) and scripts.
It previews new names as you type and can undo a rename after it runs.

It is a re-creation for modern Windows of [Flexible Renamer](https://www.vector.co.jp/soft/winnt/util/se131133.html) by Naru.

- **Install**: download `ReflexRenamer-Setup-<version>.exe` from [Releases](https://github.com/yamaotoru7/ReflexRenamer/releases). No administrator rights or separate .NET installation are needed. The installer is not code-signed, so Windows SmartScreen may warn you on first run.
- **Help**: open it from the app's **Help** menu (English and Japanese).
- **Build**: `dotnet build` with the .NET 10 SDK. See the Japanese section above for the installer and the test setup.

### Acknowledgements

Reflex Renamer is inspired by, and closely follows the design of, **Flexible Renamer** by Naru.
Its layout, workflow, replacement syntax and presets all come from Flexible Renamer, and the language files, presets and tag definitions are taken from its distribution (with the product name adjusted).
Many thanks to Naru for creating such a thoughtful tool and releasing it as free software, and to the translators of its language files: Stefan (German), Elena Lozano & F. C. A. (Spanish), Pascal FLEUTOT (French), Emiliano Bina (Italian), Luís Henrique (Brazilian Portuguese), SamWong (Simplified Chinese), rexx (Traditional Chinese) and Naru (Japanese and English).

Reflex Renamer is an independent project. It is not an official version of Flexible Renamer and is not affiliated with Naru, so please do not contact Naru about it.

### License

[MIT License](LICENSE). The data files taken from Flexible Renamer (language files, presets, tag definitions and script presets in `src/ReflexRenamer/Data/`) and the icons and bitmaps extracted from the original (everything in `src/ReflexRenamer/Assets/` except `app.ico`), and the dialog and menu definitions (`docs/spec/resources.*`) are not covered by the MIT License; they remain the work of their respective authors and are included under Flexible Renamer's free redistribution terms.
