using System.Globalization;
using System.Text;

namespace FlexibleRenamer.Core.Rename;

/// <summary>
/// 置換処理の2段目：ファイル情報と日時のタグを展開する。
/// </summary>
/// <remarks>
/// ファイル：<c>\t</c> タイトル、<c>\e</c> 拡張子、<c>\f</c> フォルダ名、<c>\F</c> 親フォルダ名、<c>\P</c> フォルダのフルパス、
/// <c>\;</c> サイズ（桁区切りあり）、<c>\:</c> サイズ。
/// 日時：strftime の <c>%</c> を <c>\</c> にしたもの。<c>\#</c> を付けると先行ゼロを削る。
/// 知らないタグは <c>\</c> を除いた文字そのものになる。
/// </remarks>
public static class TagExpander
{
    public static string Expand(string text, FileItem item, RenameOptions options)
    {
        if (!text.Contains('\\'))
        {
            return text;
        }

        var time = options.UseCreationTime ? item.Created : item.Modified;
        var culture = options.Culture;
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c != '\\' || i + 1 >= text.Length)
            {
                sb.Append(c);
                continue;
            }

            bool noZero = false;
            char n = text[++i];
            if (n == '#' && i + 1 < text.Length)
            {
                noZero = true;
                n = text[++i];
            }

            string? value = n switch
            {
                't' => item.Title,
                'e' => item.Extension,
                'f' => Path.GetFileName(item.DirectoryPath),
                'F' => Path.GetFileName(Path.GetDirectoryName(item.DirectoryPath) ?? ""),
                'P' => WithTrailingSeparator(item.DirectoryPath),
                ';' => item.Size.ToString("N0", culture),
                ':' => item.Size.ToString(CultureInfo.InvariantCulture),
                _ => DateTag(n, time, culture),
            };

            if (value is null)
            {
                sb.Append(n);
            }
            else
            {
                sb.Append(noZero ? StripLeadingZeros(value) : value);
            }
        }
        return sb.ToString();
    }

    private static string WithTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    /// <summary>strftime の変換指定子に相当する値。対応しない文字なら null。</summary>
    /// <summary>
    /// 日時だけの書式（<c>\Y/\m/\d</c> など。<c>%</c> 表記も受け付ける）を展開する。タグ・リネームの日時の書式に使う。
    /// </summary>
    public static string FormatTime(string format, DateTime t, CultureInfo culture)
    {
        var sb = new StringBuilder(format.Length);
        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            if ((c != '\\' && c != '%') || i + 1 >= format.Length)
            {
                sb.Append(c);
                continue;
            }
            bool noZero = false;
            char n = format[++i];
            if (n == '#' && i + 1 < format.Length)
            {
                noZero = true;
                n = format[++i];
            }
            string? value = DateTag(n, t, culture);
            sb.Append(value is null ? n.ToString() : noZero ? StripLeadingZeros(value) : value);
        }
        return sb.ToString();
    }

    private static string? DateTag(char c, DateTime t, CultureInfo culture)
    {
        var f = culture.DateTimeFormat;
        return c switch
        {
            'a' => f.GetAbbreviatedDayName(t.DayOfWeek),
            'A' => f.GetDayName(t.DayOfWeek),
            'b' => f.GetAbbreviatedMonthName(t.Month),
            'B' => f.GetMonthName(t.Month),
            'c' => t.ToString(f.ShortDatePattern + " " + f.LongTimePattern, culture),
            'd' => t.Day.ToString("00", CultureInfo.InvariantCulture),
            'H' => t.Hour.ToString("00", CultureInfo.InvariantCulture),
            'I' => ((t.Hour + 11) % 12 + 1).ToString("00", CultureInfo.InvariantCulture),
            'j' => t.DayOfYear.ToString("000", CultureInfo.InvariantCulture),
            'm' => t.Month.ToString("00", CultureInfo.InvariantCulture),
            'M' => t.Minute.ToString("00", CultureInfo.InvariantCulture),
            'p' => t.Hour < 12 ? f.AMDesignator : f.PMDesignator,
            'S' => t.Second.ToString("00", CultureInfo.InvariantCulture),
            'w' => ((int)t.DayOfWeek).ToString(CultureInfo.InvariantCulture),
            'W' => WeekOfYear(t, DayOfWeek.Monday).ToString("00", CultureInfo.InvariantCulture),
            'x' => t.ToString(f.ShortDatePattern, culture),
            'X' => t.ToString(f.LongTimePattern, culture),
            'y' => (t.Year % 100).ToString("00", CultureInfo.InvariantCulture),
            'Y' => t.Year.ToString(CultureInfo.InvariantCulture),
            'z' or 'Z' => TimeZoneInfo.Local.IsDaylightSavingTime(t) ? TimeZoneInfo.Local.DaylightName : TimeZoneInfo.Local.StandardName,
            '%' => "%",
            _ => null,
        };
    }

    /// <summary>strftime の %W（最初の月曜日から第1週）に相当する週番号。</summary>
    private static int WeekOfYear(DateTime t, DayOfWeek first)
    {
        int offset = ((int)first - (int)new DateTime(t.Year, 1, 1).DayOfWeek + 7) % 7;
        return (t.DayOfYear - 1 - offset + 7) / 7;
    }

    private static string StripLeadingZeros(string s)
    {
        string trimmed = s.TrimStart('0');
        return trimmed.Length == 0 && s.Length > 0 ? "0" : trimmed;
    }
}
