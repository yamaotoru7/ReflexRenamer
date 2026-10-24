using System.Text;
using System.Text.RegularExpressions;

namespace ReflexRenamer.Core.Rename;

/// <summary>
/// 置換処理の1段目：マッチ結果を置換文字列に当てはめる。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>\0</c> はマッチ全体、<c>\1</c>〜<c>\9</c> はグループ（2桁は解釈しない。<c>\12</c> はグループ1と「2」）</item>
/// <item>存在しないグループは、ワイルドカードでは数字そのもの、正規表現では空になる（実測）</item>
/// <item><c>\l \u</c> は次の1文字、<c>\L \U</c> は <c>\E</c> までを小文字・大文字にする</item>
/// <item><c>\\</c> は <c>\</c> 1文字になり、次の文字と合わせて2段目のタグとして解釈される（<c>a\\b</c> → <c>a\b</c> → 月名）</item>
/// <item>それ以外の <c>\x</c> は2段目のためにそのまま残す</item>
/// </list>
/// </remarks>
public static class ReplacementFormatter
{
    private enum CaseMode
    {
        None,
        Upper,
        Lower,
    }

    public static string Format(string replacement, Match match, bool missingGroupAsDigit)
    {
        var sb = new StringBuilder();
        var mode = CaseMode.None;
        var once = CaseMode.None;

        void Append(string text)
        {
            foreach (char ch in text)
            {
                var m = once != CaseMode.None ? once : mode;
                once = CaseMode.None;
                sb.Append(m switch
                {
                    CaseMode.Upper => char.ToUpperInvariant(ch),
                    CaseMode.Lower => char.ToLowerInvariant(ch),
                    _ => ch,
                });
            }
        }

        for (int i = 0; i < replacement.Length; i++)
        {
            char c = replacement[i];
            if (c != '\\' || i + 1 >= replacement.Length)
            {
                Append(c.ToString());
                continue;
            }

            char n = replacement[++i];
            switch (n)
            {
                case >= '0' and <= '9':
                {
                    int g = n - '0';
                    if (g < match.Groups.Count)
                    {
                        Append(match.Groups[g].Value);
                    }
                    else if (missingGroupAsDigit)
                    {
                        Append(n.ToString());
                    }
                    break;
                }
                case 'l':
                    once = CaseMode.Lower;
                    break;
                case 'u':
                    once = CaseMode.Upper;
                    break;
                case 'L':
                    mode = CaseMode.Lower;
                    break;
                case 'U':
                    mode = CaseMode.Upper;
                    break;
                case 'E':
                    mode = CaseMode.None;
                    break;
                case '\\':
                    sb.Append('\\');
                    break;
                default:
                    sb.Append('\\').Append(n);
                    break;
            }
        }
        return sb.ToString();
    }
}
