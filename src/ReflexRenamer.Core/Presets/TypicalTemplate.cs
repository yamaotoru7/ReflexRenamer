using System.Globalization;
using System.Text.RegularExpressions;
using ReflexRenamer.Core.Localization;

namespace ReflexRenamer.Core.Presets;

/// <summary>定型メニューのパラメータ欄（DIALOG 146）の入力値。既定値はオリジナル版の初期状態。</summary>
public sealed record TypicalParameters
{
    /// <summary>追加文字列（<c>\ADD</c>）。日時のタグも書ける。</summary>
    public string AddString { get; init; } = "";

    /// <summary>開始番号。</summary>
    public long StartNumber { get; init; }

    /// <summary>桁数（<c>\NUM</c> の <c>?</c> の数、<c>\DIGVALUE</c>）。</summary>
    public int Digits { get; init; } = 3;

    /// <summary>位置（<c>\POS</c>）。</summary>
    public int Position { get; init; }

    /// <summary>値（<c>\NUMVALUE</c>）。</summary>
    public long Value { get; init; }

    /// <summary>日時の書式（<c>\TIMESTAMP</c>）。</summary>
    public string TimeFormat { get; init; } = @"\Y\m\d\H\M\S";

    /// <summary>置換前の文字列（<c>\SRC</c>）。</summary>
    public string Source { get; init; } = "";

    /// <summary>置換後の文字列（<c>\DST</c>）。</summary>
    public string Destination { get; init; } = "";
}

/// <summary>定型メニューを展開した結果（高度なリネームの設定に相当する）。</summary>
public sealed record ExpandedTypical(RenameMode Mode, string Script, string Search, string Replace, long NumberStart);

/// <summary>
/// 定型メニュー（Preset.ini の <c>[Typical]</c>）のプレースホルダをパラメータで埋める。
/// </summary>
public static class TypicalTemplate
{
    public static ExpandedTypical Expand(TypicalPreset typical, TypicalParameters p, LanguageStrings? strings)
    {
        string source = typical.Mode == RenameMode.RegularExpression ? EscapeRegex(p.Source) : p.Source;

        string search = Fill(typical.Search, p, source);
        string replace = Fill(typical.Replace, p, source);
        if (strings is not null)
        {
            search = strings.Expand(search);
            replace = strings.Expand(replace);
        }
        return new ExpandedTypical(typical.Mode, typical.Script, search, replace, p.StartNumber);
    }

    /// <summary>プレースホルダを置き換える。長いものから順に置き換えないと <c>\NUMVALUE</c> が <c>\NUM</c> に食われる。</summary>
    private static string Fill(string text, TypicalParameters p, string source) => text
        .Replace(@"\NUMVALUE", p.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace(@"\DIGVALUE", p.Digits.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace(@"\TIMESTAMP", p.TimeFormat, StringComparison.Ordinal)
        .Replace(@"\NUM", new string('?', Math.Max(p.Digits, 0)), StringComparison.Ordinal)
        .Replace(@"\ADD", p.AddString, StringComparison.Ordinal)
        .Replace(@"\POS", p.Position.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace(@"\SRC", source, StringComparison.Ordinal)
        .Replace(@"\DST", p.Destination, StringComparison.Ordinal);

    private static string EscapeRegex(string s) => Regex.Escape(s);
}
