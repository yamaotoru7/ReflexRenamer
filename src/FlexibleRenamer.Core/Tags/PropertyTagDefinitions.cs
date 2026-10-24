using System.Globalization;
using System.Text;

namespace FlexibleRenamer.Core.Tags;

/// <summary>
/// 「ファイルプロパティ情報を使う」のプリセット。<c>Columns</c> はエクスプローラーの「詳細」の列番号
/// （<c>Folder.GetDetailsOf</c> の番号）で、書式では列の表示名を山かっこで囲んで書く（<c>&lt;タイトル&gt;</c>）。
/// </summary>
public sealed record PropertyPreset(string Name, string Filter, IReadOnlyList<int> Columns)
{
    /// <summary>フィルタ（<c>mp3;wma</c>）の拡張子。</summary>
    public IReadOnlyList<string> Extensions => Filter.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>プロパティ定義ダイアログで列を分けるグループ。<c>Columns</c> が <c>-1</c> だけのものは「その他」（どのグループにもない列）。</summary>
public sealed record PropertyGroup(string Name, IReadOnlyList<int> Columns)
{
    public bool IsOther => Columns.Count == 1 && Columns[0] == -1;
}

/// <summary>
/// <c>TagDef.ini</c> の <c>[PropertyTagTarget]</c>、<c>[PropertyTag-&lt;ver&gt;]</c>、<c>[PropertyTagGroup-&lt;ver&gt;]</c>。
/// 列番号は Windows のバージョンで変わるので、表はバージョンごとにある。オリジナル版は使った表の番号を
/// ini の <c>OSVersion_PropertyTag</c> に残す（Windows 11 では最大の 6110）。
/// </summary>
public sealed class PropertyTagDefinitions
{
    public required int Version { get; init; }

    public required IReadOnlyList<PropertyPreset> Presets { get; init; }

    public required IReadOnlyList<PropertyGroup> Groups { get; init; }

    public static PropertyTagDefinitions Load(string path, int? osVersion = null)
    {
        var sections = ReadSections(path);
        var targets = sections.TryGetValue("PropertyTagTarget", out var t)
            ? t.Select(line => line.Split('=', 2)).Where(p => p.Length == 2 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                .Select(p => int.Parse(p[0], CultureInfo.InvariantCulture)).OrderBy(v => v).ToList()
            : [];
        // 動作中の Windows 以下で最大の表。どれにも当てはまらなければ最大の表（Windows 11 なら 6110）
        int version = targets.Count == 0 ? 0 : osVersion is { } os && targets.Any(v => v <= os) ? targets.Last(v => v <= os) : targets[^1];
        return new PropertyTagDefinitions
        {
            Version = version,
            Presets = ParsePresets(sections.TryGetValue("PropertyTag-" + version, out var p) ? p : []),
            Groups = sections.TryGetValue("PropertyTagGroup-" + version, out var g)
                ? g.Select(ParseGroup).Where(x => x is not null).Select(x => x!).ToList()
                : [],
        };
    }

    /// <summary>
    /// <c>Preset=&lt;番号&gt;⇥⇥&lt;フィルタ&gt;⇥&lt;列番号,...&gt;⇥&lt;名前&gt;</c> の行（TagDef.ini でも ini の <c>[TagRename_PropertyTag]</c> でも同じ）。
    /// 番号は大きい順に書かれているので、番号順に並べ直す。
    /// </summary>
    public static IReadOnlyList<PropertyPreset> ParsePresets(IEnumerable<string> lines)
    {
        var list = new List<(int Order, PropertyPreset Preset)>();
        foreach (var line in lines)
        {
            if (!line.StartsWith("Preset=", StringComparison.Ordinal))
            {
                continue;
            }
            var parts = line["Preset=".Length..].Split('\t');
            if (parts.Length < 5)
            {
                continue;
            }
            int order = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int o) ? o : int.MaxValue;
            list.Add((order, new PropertyPreset(parts[4], parts[2], ParseColumns(parts[3]))));
        }
        return list.OrderBy(x => x.Order).Select(x => x.Preset).ToList();
    }

    /// <summary>ini に保存する形式（番号は大きい順）。</summary>
    public static IEnumerable<string> FormatPresets(IReadOnlyList<PropertyPreset> presets)
    {
        for (int i = presets.Count - 1; i >= 0; i--)
        {
            var p = presets[i];
            yield return $"Preset={(i + 1).ToString(CultureInfo.InvariantCulture)}\t\t{p.Filter}\t{string.Join(',', p.Columns.Select(c => c.ToString(CultureInfo.InvariantCulture)))}\t{p.Name}";
        }
    }

    private static PropertyGroup? ParseGroup(string line)
    {
        if (!line.StartsWith("Group=", StringComparison.Ordinal))
        {
            return null;
        }
        var parts = line["Group=".Length..].Split('\t');
        return parts.Length < 2 ? null : new PropertyGroup(parts[0], ParseColumns(parts[1]));
    }

    private static IReadOnlyList<int> ParseColumns(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : (int?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

    private static Dictionary<string, List<string>> ReadSections(string path)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? section = null;
        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }
            if (line.StartsWith('['))
            {
                section = line.Trim('[', ']');
                sections[section] = [];
            }
            else if (section is not null)
            {
                sections[section].Add(raw.TrimEnd('\r', '\n'));
            }
        }
        return sections;
    }
}
