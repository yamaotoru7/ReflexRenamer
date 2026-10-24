using System.Text;

namespace FlexibleRenamer.Core.Rename;

/// <summary>
/// 文字変換（Perl の <c>tr/検索リスト/置換リスト/</c> 相当）。
/// </summary>
/// <remarks>
/// オリジナル版の実測に合わせて次のようにする。
/// <list type="bullet">
/// <item><c>a-z</c> のような範囲は、コードポイント順にそのまま展開する</item>
/// <item>置換リストが短いときは最後の文字で埋める。空なら該当する文字を削除する</item>
/// <item>タイトルと拡張子を別々に変換し、区切りのドットには触れない</item>
/// <item>拡張子は、変換結果がファイル名に使える ASCII 文字だけになる場合に限って変換する</item>
/// </list>
/// </remarks>
public sealed class Translator
{
    private readonly Dictionary<char, char?> _map = [];

    public Translator(string search, string replace)
    {
        var from = Expand(search);
        var to = Expand(replace);
        for (int i = 0; i < from.Count; i++)
        {
            if (_map.ContainsKey(from[i]))
            {
                continue;
            }
            _map[from[i]] = to.Count == 0 ? null : to[Math.Min(i, to.Count - 1)];
        }
    }

    public string Translate(string name)
    {
        int dot = FileNameParts.ExtensionStart(name);
        if (dot < 0)
        {
            return Map(name);
        }

        string title = Map(name[..dot]);
        string ext = name[(dot + 1)..];
        string mappedExt = Map(ext);
        return title + "." + (IsSafeExtension(mappedExt) ? mappedExt : ext);
    }

    private string Map(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (_map.TryGetValue(c, out var to))
            {
                if (to is { } t)
                {
                    sb.Append(t);
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static bool IsSafeExtension(string ext) =>
        ext.All(c => c is >= ' ' and <= '~' && "\\/:*?\"<>|".IndexOf(c) < 0);

    /// <summary>リストを文字の並びに展開する。<c>x-y</c> は範囲。先頭・末尾の <c>-</c> は文字そのもの。</summary>
    public static List<char> Expand(string list)
    {
        var chars = new List<char>();
        for (int i = 0; i < list.Length; i++)
        {
            char c = list[i];
            if (i + 2 < list.Length && list[i + 1] == '-')
            {
                char end = list[i + 2];
                for (int code = c; code <= end; code++)
                {
                    chars.Add((char)code);
                }
                i += 2;
            }
            else
            {
                chars.Add(c);
            }
        }
        return chars;
    }
}
