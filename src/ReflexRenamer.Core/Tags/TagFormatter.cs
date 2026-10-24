using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ReflexRenamer.Core.Rename;

namespace ReflexRenamer.Core.Tags;

/// <summary>タグ・リネームの設定（DIALOG 172）：日時の書式と、無効な文字の置き換え。</summary>
public sealed record TagFormatOptions
{
    /// <summary>日時の書式の既定値（オリジナル版の設定ダイアログの初期値）。</summary>
    public const string DefaultDateTimeFormat = @"\Y\m\d\H\M\S";

    /// <summary>日時の書式。リネームの日時タグと同じ <c>\Y\m\d</c> 形式（<c>%</c> 表記も可）。</summary>
    public string DateTimeFormat { get; init; } = DefaultDateTimeFormat;

    /// <summary>ファイル名に使えない文字（<c>\ / : * ? " &lt; &gt; |</c>）をどう置き換えるか。</summary>
    public IReadOnlyDictionary<char, string> InvalidCharReplacements { get; init; } = DefaultReplacements;

    public static readonly IReadOnlyDictionary<char, string> DefaultReplacements = new Dictionary<char, string>
    {
        // 既定値はオリジナル版の設定ダイアログの初期値（explore-tagrename の settings）
        ['\\'] = "$", ['/'] = "%", [':'] = ";", ['*'] = "#", ['?'] = "@", ['"'] = "''", ['<'] = "[", ['>'] = "]", ['|'] = "!",
    };

    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;
}

/// <summary>
/// 書式の中の <c>&lt;ID3.Artist&gt;</c> のようなタグをファイルの値に置き換える。
/// </summary>
public static partial class TagFormatter
{
    [GeneratedRegex(@"<([A-Za-z0-9]+(?:\.[A-Za-z0-9\-]+)?)>")]
    private static partial Regex Token();

    /// <summary>プロパティモードのタグ。列の表示名（<c>&lt;ビット レート&gt;</c>）なので、山かっこ以外なら何でもよい。</summary>
    [GeneratedRegex(@"<([^<>]+)>")]
    private static partial Regex AnyToken();

    /// <summary>
    /// タグを展開する。<paramref name="lookup"/> は言語ファイルの対応表（<c>&lt;EXIF.Flash&gt;(0001)</c> → 名前）を引く関数で、なければ null を返す。
    /// <paramref name="isKnown"/> は選んでいる種類（TagDef.ini）にそのタグがあるかを返す。ないタグは文字のまま、あって値のないタグは空になる（正解データ music-unknown-tag）。
    /// </summary>
    public static string Expand(string format, TagSet tags, TagFormatOptions options, Func<string, string?>? lookup = null, Func<string, bool>? isKnown = null, bool anyName = false)
    {
        if (!format.Contains('<'))
        {
            return format;
        }
        return (anyName ? AnyToken() : Token()).Replace(format, m =>
        {
            string name = m.Groups[1].Value;
            if (isKnown is not null && !isKnown(name))
            {
                return m.Value;
            }
            var value = tags[name];
            string text = value.Time is { } time
                ? TagExpander.FormatTime(options.DateTimeFormat, time, options.Culture)
                : FormatValue(name, value.Text ?? "", lookup);
            return ReplaceInvalidChars(text, options.InvalidCharReplacements);
        });
    }

    /// <summary>列挙値や数値の表示。言語ファイルの <c>&lt;Tag&gt;(code)</c> の対応を使う。</summary>
    private static string FormatValue(string name, string raw, Func<string, string?>? lookup)
    {
        if (lookup is null || raw.Length == 0)
        {
            return raw;
        }
        string token = "<" + name + ">";
        switch (name.ToUpperInvariant())
        {
            case "EXIF.EXPOSURETIME":
            {
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
                {
                    return raw;
                }
                string? f = seconds > 0.5 ? lookup(token + "(gt 0.5)") : lookup(token + "(le 0.5)");
                if (f is null)
                {
                    return raw;
                }
                return seconds > 0.5
                    ? Printf(f, seconds)
                    : Printf(f, (int)Math.Round(1 / seconds));
            }
            case "EXIF.ISOSPEED":
                // 言語ファイルに書式はなく、オリジナル版は常に「ISO-400」の形にする（正解データ picture-values）
                return "ISO-" + raw;
            case "EXIF.EXPOSURECOMPENSATION":
            {
                string? f = lookup(token + "(Format)");
                return f is null || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? raw : Printf(f, (int)Math.Round(v));
            }
            default:
            {
                // 4桁16進の列挙値なら対応表、なければ (Default)
                if (raw.Length == 4 && raw.All(Uri.IsHexDigit))
                {
                    return lookup(token + "(" + raw + ")") ?? lookup(token + "(Default)") ?? raw;
                }
                return raw;
            }
        }
    }

    /// <summary>C の printf 風の書式（<c>%d</c>、<c>%1.2f</c>、<c>%s</c>）。</summary>
    private static string Printf(string format, object value) =>
        Regex.Replace(format, @"%(\d*)(?:\.(\d+))?([dfs])", m =>
        {
            string spec = m.Groups[3].Value;
            string s = spec switch
            {
                "d" => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                "f" => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("F" + (m.Groups[2].Success ? m.Groups[2].Value : "6"), CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "",
            };
            return m.Groups[1].Success && m.Groups[1].Value.Length > 0 && int.TryParse(m.Groups[1].Value, out int width) ? s.PadLeft(width) : s;
        });

    public static string ReplaceInvalidChars(string text, IReadOnlyDictionary<char, string> replacements)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (replacements.TryGetValue(c, out var r))
            {
                sb.Append(r);
            }
            else if (c < ' ')
            {
                // 制御文字は捨てる
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Trim();
    }
}
