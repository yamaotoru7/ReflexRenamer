using System.Globalization;
using FlexibleRenamer.Core.Presets;

namespace FlexibleRenamer.Core.Rename;

/// <summary>連番の表記（番号の種類ダイアログ）。</summary>
public enum NumberType
{
    Decimal,
    Hexadecimal,
    Alphabetical,
}

/// <summary>
/// 高度なリネームの設定。
/// </summary>
public sealed record RenameOptions
{
    public RenameMode Mode { get; init; } = RenameMode.Wildcard;

    public string Search { get; init; } = "";

    public string Replace { get; init; } = "";

    /// <summary>正規表現オプション「大文字と小文字を区別する検索」。既定はオフ。</summary>
    public bool CaseSensitive { get; init; }

    /// <summary>正規表現オプション「完全一致のみ表示」。既定はオフ。</summary>
    public bool CompleteMatchOnly { get; init; }

    public long NumberStart { get; init; }

    public long NumberStep { get; init; } = 1;

    public NumberType NumberType { get; init; } = NumberType.Decimal;

    /// <summary>「フォルダごとにリセット」。フォルダが変わるたびに連番を開始番号に戻す。</summary>
    public bool ResetNumberByFolder { get; init; }

    /// <summary>一覧をグループ表示しているとき、グループが変わるたびに連番を戻すためのキー。null ならグループなし。</summary>
    public Func<FileItem, string>? NumberGroupKey { get; init; }

    /// <summary>タイムスタンプに作成日時を使う。既定は更新日時。</summary>
    public bool UseCreationTime { get; init; }

    /// <summary>曜日名や日付の書式に使うカルチャ。既定は現在のカルチャ（ユーザー設定を含む）。</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;

    /// <summary>
    /// スクリプトの評価（置換欄の「スクリプト」がオンのとき）。タグを展開した後の置換結果を式として評価し、結果を新しい名前にする。
    /// 連番と乱数はスクリプトの後で展開する（v8.2 の修正「スクリプトで演算子 * を使うとランダム数字になる」より推定。未検証）。
    /// </summary>
    public Func<string, string>? Script { get; init; }

    /// <summary>
    /// タグ・リネーム用：置換（書式）の中の <c>&lt;ID3.Artist&gt;</c> のようなタグを、そのファイルの値に置き換える。
    /// パターン置換より前に、ファイルごとに行う。
    /// </summary>
    public Func<FileItem, string, string>? TagExpander { get; init; }

    /// <summary><c>*</c>（ランダムな数字）に使う乱数。テストで固定するため差し替えられる。</summary>
    public Random Random { get; init; } = Random.Shared;
}
