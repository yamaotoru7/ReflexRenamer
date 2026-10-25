using System.Text.Json;

namespace ReflexRenamer.Core.Resources;

/// <summary>ダイアログテンプレートの1コントロール。座標はダイアログ単位（DLU）。</summary>
public sealed record DialogControl(int Id, string Class, string Text, int X, int Y, int Width, int Height, uint Style)
{
    public bool Visible => (Style & 0x10000000) != 0;

    public bool Disabled => (Style & 0x08000000) != 0;
}

public sealed record DialogTemplate(string Title, int Width, int Height, uint Style, string Font, IReadOnlyList<DialogControl> Controls);

/// <summary>メニュー項目。Children があればサブメニュー。</summary>
public sealed record MenuTemplate(int Id, string Text, bool Separator, IReadOnlyList<MenuTemplate>? Children);

public sealed record AcceleratorTemplate(int Command, string Key);

/// <summary>
/// オリジナル版の exe から抽出したリソース（<c>docs/spec/resources.json</c>、<c>FrTools res-dump</c> で生成）。
/// 移植版はこれをもとに画面を組み立てる。
/// </summary>
public sealed class ResourceSet
{
    private ResourceSet(
        Dictionary<int, DialogTemplate> dialogs,
        Dictionary<int, IReadOnlyList<MenuTemplate>> menus,
        Dictionary<int, IReadOnlyList<AcceleratorTemplate>> accelerators)
    {
        Dialogs = dialogs;
        Menus = menus;
        Accelerators = accelerators;
    }

    public IReadOnlyDictionary<int, DialogTemplate> Dialogs { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<MenuTemplate>> Menus { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<AcceleratorTemplate>> Accelerators { get; }

    public static ResourceSet Load(Stream json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dialogs = new Dictionary<int, DialogTemplate>();
        foreach (var d in root.GetProperty("Dialogs").EnumerateArray())
        {
            var v = d.GetProperty("Value");
            var controls = v.GetProperty("Controls").EnumerateArray().Select(c => new DialogControl(
                c.GetProperty("Id").GetInt32(),
                c.GetProperty("Class").GetString()!,
                c.GetProperty("Text").GetString()!,
                c.GetProperty("X").GetInt32(),
                c.GetProperty("Y").GetInt32(),
                c.GetProperty("Width").GetInt32(),
                c.GetProperty("Height").GetInt32(),
                c.GetProperty("Style").GetUInt32())).ToList();
            dialogs[int.Parse(d.GetProperty("Id").GetString()!)] = new DialogTemplate(
                v.GetProperty("Title").GetString()!,
                v.GetProperty("Width").GetInt32(),
                v.GetProperty("Height").GetInt32(),
                v.GetProperty("Style").GetUInt32(),
                v.GetProperty("Font").GetString()!,
                controls);
        }

        var menus = new Dictionary<int, IReadOnlyList<MenuTemplate>>();
        foreach (var m in root.GetProperty("Menus").EnumerateArray())
        {
            menus[int.Parse(m.GetProperty("Id").GetString()!)] = ReadMenu(m.GetProperty("Value"));
        }

        var accelerators = new Dictionary<int, IReadOnlyList<AcceleratorTemplate>>();
        foreach (var a in root.GetProperty("Accelerators").EnumerateArray())
        {
            accelerators[int.Parse(a.GetProperty("Id").GetString()!)] = a.GetProperty("Value").EnumerateArray()
                .Select(k => new AcceleratorTemplate(k.GetProperty("Command").GetInt32(), k.GetProperty("Key").GetString()!))
                .ToList();
        }

        return new ResourceSet(dialogs, menus, accelerators);
    }

    /// <summary>
    /// メニューからコマンドを除いた ResourceSet を返す（移植版で使わない項目。オリジナル版のホームページやメールなど）。
    /// 除いた結果、先頭・末尾・連続になった区切り線も除く。
    /// </summary>
    public ResourceSet WithoutCommands(params int[] commands)
    {
        var removed = commands.ToHashSet();
        var menus = Menus.ToDictionary(m => m.Key, m => (IReadOnlyList<MenuTemplate>)Filter(m.Value, removed));
        return new ResourceSet(Dialogs.ToDictionary(), menus, Accelerators.ToDictionary());
    }

    private static List<MenuTemplate> Filter(IReadOnlyList<MenuTemplate> items, HashSet<int> removed)
    {
        var result = new List<MenuTemplate>();
        foreach (var item in items)
        {
            if (item.Separator)
            {
                if (result.Count > 0 && !result[^1].Separator)
                {
                    result.Add(item);
                }
            }
            else if (item.Children is not null)
            {
                result.Add(item with { Children = Filter(item.Children, removed) });
            }
            else if (!removed.Contains(item.Id))
            {
                result.Add(item);
            }
        }
        if (result.Count > 0 && result[^1].Separator)
        {
            result.RemoveAt(result.Count - 1);
        }
        return result;
    }

    private static List<MenuTemplate> ReadMenu(JsonElement items) => items.EnumerateArray()
        .Select(i => new MenuTemplate(
            i.GetProperty("Id").GetInt32(),
            i.GetProperty("Text").GetString()!,
            i.GetProperty("Separator").GetBoolean(),
            i.GetProperty("Children").ValueKind == JsonValueKind.Array ? ReadMenu(i.GetProperty("Children")) : null))
        .ToList();
}
