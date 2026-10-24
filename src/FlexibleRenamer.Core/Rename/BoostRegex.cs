using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FlexibleRenamer.Core.Rename;

/// <summary>
/// オリジナル版の正規表現（boost regex、Perl 互換）を .NET の正規表現に変換する。
/// .NET にない書き方だけを置き換え、それ以外はそのまま渡す。
/// </summary>
public static class BoostRegex
{
    private static readonly Dictionary<string, string> PosixClasses = new()
    {
        ["alnum"] = @"\p{L}\p{Nd}",
        ["alpha"] = @"\p{L}",
        ["blank"] = @" \t",
        ["cntrl"] = @"\p{Cc}",
        ["digit"] = @"\d",
        ["graph"] = @"\p{L}\p{M}\p{N}\p{P}\p{S}",
        ["lower"] = @"\p{Ll}",
        ["print"] = @"\p{L}\p{M}\p{N}\p{P}\p{S}\p{Zs}",
        ["punct"] = @"\p{P}",
        ["space"] = @"\s",
        ["upper"] = @"\p{Lu}",
        ["word"] = @"\w",
        ["xdigit"] = @"0-9A-Fa-f",
    };

    /// <summary>変換できない、または .NET で不正な正規表現なら null を返す。</summary>
    public static Regex? TryCreate(string pattern, bool caseSensitive)
    {
        var options = RegexOptions.CultureInvariant;
        if (!caseSensitive)
        {
            options |= RegexOptions.IgnoreCase;
        }
        try
        {
            return new Regex(Convert(pattern), options);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static string Convert(string pattern)
    {
        var sb = new StringBuilder(pattern.Length);
        bool inClass = false;
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                char n = pattern[i + 1];
                switch (n)
                {
                    case '<' when !inClass:
                        sb.Append(@"\b(?=\w)");
                        i++;
                        continue;
                    case '>' when !inClass:
                        sb.Append(@"\b(?<=\w)");
                        i++;
                        continue;
                    case 'C' when !inClass:
                        sb.Append("[\\s\\S]");
                        i++;
                        continue;
                    case 'x' when i + 2 < pattern.Length && pattern[i + 2] == '{':
                    {
                        int close = pattern.IndexOf('}', i + 3);
                        if (close > 0 && int.TryParse(pattern.AsSpan(i + 3, close - i - 3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                        {
                            sb.Append(Regex.Escape(char.ConvertFromUtf32(code)));
                            i = close;
                            continue;
                        }
                        break;
                    }
                    case 'Q':
                    {
                        int end = pattern.IndexOf(@"\E", i + 2, StringComparison.Ordinal);
                        string literal = end < 0 ? pattern[(i + 2)..] : pattern[(i + 2)..end];
                        sb.Append(inClass ? EscapeInClass(literal) : Regex.Escape(literal));
                        i = end < 0 ? pattern.Length : end + 1;
                        continue;
                    }
                }
                sb.Append(c).Append(n);
                i++;
                continue;
            }

            if (inClass)
            {
                if (c == '[' && i + 1 < pattern.Length && pattern[i + 1] == ':')
                {
                    int close = pattern.IndexOf(":]", i + 2, StringComparison.Ordinal);
                    if (close > 0)
                    {
                        string name = pattern[(i + 2)..close];
                        bool negate = name.StartsWith('^');
                        if (!negate && PosixClasses.TryGetValue(name, out var expansion))
                        {
                            sb.Append(expansion);
                            i = close + 1;
                            continue;
                        }
                    }
                }
                if (c == ']')
                {
                    inClass = false;
                }
                sb.Append(c);
                continue;
            }

            if (c == '[')
            {
                inClass = true;
                sb.Append(c);
                // 先頭の ^ と ] はクラスの一部
                if (i + 1 < pattern.Length && pattern[i + 1] == '^')
                {
                    sb.Append('^');
                    i++;
                }
                if (i + 1 < pattern.Length && pattern[i + 1] == ']')
                {
                    sb.Append(@"\]");
                    i++;
                }
                continue;
            }

            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string EscapeInClass(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (c is '\\' or ']' or '[' or '^' or '-')
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
