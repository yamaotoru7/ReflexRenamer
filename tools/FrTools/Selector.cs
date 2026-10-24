using System.Text.Json;
using FlaUI.Core.AutomationElements;

namespace FrTools;

/// <summary>
/// 要素の指定。<c>id:1010</c>、<c>name:リネーム(R)</c>、<c>type:Button</c>、<c>class:Edit</c> を
/// カンマでつなぐと AND 条件になる。<c>window:Numbering</c> を付けるとそのタイトルのトップレベルウィンドウ内だけを探す。
/// 移植版では、元のリソースの ID と同じ値を Control.Name に設定する決まりなので、<c>id:</c> は両方で共通に使える。
/// </summary>
internal sealed class Selector
{
    private readonly List<(string Key, string Value)> _parts = [];

    public Selector(string text)
    {
        Text = text;
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = part.IndexOf(':');
            if (colon < 0) throw new FormatException($"セレクタの書式が違う: {part}");
            _parts.Add((part[..colon], part[(colon + 1)..]));
        }
    }

    public string Text { get; }

    /// <summary>
    /// シナリオ JSON の target を読む。文字列なら両方共通、
    /// <c>{"original": "...", "clone": "..."}</c> ならアプリごとに使い分ける。
    /// </summary>
    public static Selector? From(JsonElement? target, AppKind kind)
    {
        if (target is not { } t || t.ValueKind == JsonValueKind.Undefined || t.ValueKind == JsonValueKind.Null) return null;
        if (t.ValueKind == JsonValueKind.String) return new Selector(t.GetString()!);
        string key = kind == AppKind.Original ? "original" : "clone";
        return t.TryGetProperty(key, out var v) ? new Selector(v.GetString()!) : null;
    }

    public AutomationElement Find(AppSession session, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (true)
        {
            var found = TryFind(session);
            if (found is not null) return found;
            if (DateTime.UtcNow > deadline)
            {
                string windows = string.Join(" / ", session.AllWindows().Select(w => $"'{w.Properties.Name.ValueOrDefault}'"));
                throw new InvalidOperationException($"要素が見つからない: {Text}（トップレベルウィンドウ: {windows}）");
            }
            Thread.Sleep(200);
        }
    }

    private AutomationElement? TryFind(AppSession session)
    {
        string? window = _parts.FirstOrDefault(p => p.Key == "window").Value;
        // タイトルが完全に一致するウィンドウ（メッセージボックスの「Flexible Renamer」）を、部分一致（メイン「Flexible Renamer v8.4」）より先に探す
        var windows = window is null
            ? session.AllWindows()
            : session.AllWindows().OrderBy(w => (w.Properties.Name.ValueOrDefault ?? "") == window ? 0 : 1).ToList();
        foreach (var top in windows)
        {
            if (window is not null && !(top.Properties.Name.ValueOrDefault ?? "").Contains(window, StringComparison.Ordinal)) continue;
            if (Matches(top)) return top;
            foreach (var e in top.FindAllDescendants())
            {
                if (Matches(e)) return e;
            }
        }
        return null;
    }

    private bool Matches(AutomationElement e)
    {
        foreach (var (key, value) in _parts)
        {
            var p = e.Properties;
            bool ok = key switch
            {
                "window" => true,
                "id" => p.AutomationId.ValueOrDefault == value,
                "name" => p.Name.ValueOrDefault == value,
                "name~" => (p.Name.ValueOrDefault ?? "").Contains(value, StringComparison.Ordinal),
                // WinForms の ListView（詳細表示）は Table として見えるので、List と同じ扱いにする
                "type" => p.ControlType.ValueOrDefault.ToString() is var type && (type == value || (value == "List" && type == "Table")),
                "class" => p.ClassName.ValueOrDefault == value,
                _ => throw new FormatException($"未知のセレクタ種別: {key}"),
            };
            if (!ok) return false;
        }
        return true;
    }
}
