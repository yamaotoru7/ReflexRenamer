using System.Text;
using System.Text.RegularExpressions;

namespace FlexibleRenamer.Core.Rename;

/// <summary>
/// ワイルドカードを正規表現に変換する。オリジナル版の実測に合わせて次のようにする。
/// <list type="bullet">
/// <item><c>*</c> は最短一致のグループ <c>(.*?)</c></item>
/// <item>連続する <c>?</c> はまとめて1つのグループ <c>(.{n})</c></item>
/// <item>名前全体との一致で、大文字小文字は区別しない</item>
/// </list>
/// </summary>
public static class WildcardPattern
{
    public static Regex ToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '*')
            {
                sb.Append("(.*?)");
            }
            else if (c == '?')
            {
                int run = 1;
                while (i + 1 < pattern.Length && pattern[i + 1] == '?')
                {
                    run++;
                    i++;
                }
                sb.Append("(.{").Append(run).Append("})");
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
            }
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }
}
