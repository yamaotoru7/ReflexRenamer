using System.Text;

namespace ReflexRenamer.Core.Tags;

/// <summary>タグの種類（TagDef.ini のセクション）。</summary>
public sealed record TagKind(string Section, string Caption, IReadOnlyList<string> Extensions, IReadOnlyList<TagField> Fields);

/// <summary>タグ1つ。<c>Name</c> は <c>ID3.Artist</c> のような山かっこなしの名前、<c>Submenu</c> はタグ一覧メニューでのサブメニュー名（<c>v1</c> など）。</summary>
public sealed record TagField(string Name, string? Submenu)
{
    /// <summary>書式に書くときの表記（<c>&lt;ID3.Artist&gt;</c>）。</summary>
    public string Token => "<" + Name + ">";
}

/// <summary>
/// <c>TagDef.ini</c>（ASCII）の読み込み。種類ごとに、対象の拡張子と使えるタグの一覧を持つ。
/// </summary>
public static class TagDefinitions
{
    public static readonly string[] KindSections = ["MusicTag", "PictureTag", "ImageTag", "DocumentTag", "WebPageTag"];

    public static IReadOnlyList<TagKind> Load(string path)
    {
        var kinds = new List<TagKind>();
        string? section = null;
        string caption = "";
        var extensions = new List<string>();
        var fields = new List<TagField>();

        void Flush()
        {
            if (section is not null && KindSections.Contains(section))
            {
                kinds.Add(new TagKind(section, caption, extensions.ToList(), fields.ToList()));
            }
        }

        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }
            if (line.StartsWith('['))
            {
                Flush();
                section = line.Trim('[', ']');
                caption = "";
                extensions.Clear();
                fields.Clear();
                continue;
            }
            if (line.StartsWith("Caption=", StringComparison.Ordinal))
            {
                caption = line["Caption=".Length..];
            }
            else if (line.StartsWith("Extensions=", StringComparison.Ordinal))
            {
                extensions.AddRange(line["Extensions=".Length..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            else if (line.Contains('<'))
            {
                // 「v2\<ID3v2.TALB>」のように、バックスラッシュの前がサブメニュー名
                int lt = line.IndexOf('<');
                string? submenu = lt > 0 ? line[..lt].TrimEnd('\\') : null;
                fields.Add(new TagField(line[(lt + 1)..].TrimEnd('>'), submenu));
            }
        }
        Flush();
        return kinds;
    }
}
