using System.Drawing;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FrTools;

/// <summary>
/// UI Automation のツリーをテキストに書き出す。1行1要素で、インデントが階層を表す。
/// 座標はメインウィンドウ左上からの相対値なので、2つのアプリの出力を並べて比べられる。
/// </summary>
internal static class UiTree
{
    private const int MaxChildren = 200;

    public static string Dump(AutomationElement root)
    {
        var origin = root.BoundingRectangle.Location;
        var sb = new StringBuilder();
        Walk(root, origin, 0, sb);
        return sb.ToString();
    }

    private static void Walk(AutomationElement e, Point origin, int depth, StringBuilder sb)
    {
        sb.Append(' ', depth * 2).AppendLine(Describe(e, origin));

        AutomationElement[] children;
        try
        {
            children = e.FindAllChildren();
        }
        catch (Exception)
        {
            return;
        }

        foreach (var child in children.Take(MaxChildren))
        {
            Walk(child, origin, depth + 1, sb);
        }
        if (children.Length > MaxChildren)
        {
            sb.Append(' ', (depth + 1) * 2).AppendLine($"... 他 {children.Length - MaxChildren} 件");
        }
    }

    public static string Describe(AutomationElement e, Point origin)
    {
        var p = e.Properties;
        string type = p.ControlType.ValueOrDefault.ToString();
        string name = p.Name.ValueOrDefault ?? "";
        string id = p.AutomationId.ValueOrDefault ?? "";
        string cls = p.ClassName.ValueOrDefault ?? "";
        var r = p.BoundingRectangle.ValueOrDefault;
        bool enabled = p.IsEnabled.ValueOrDefault;
        bool offscreen = p.IsOffscreen.ValueOrDefault;

        var sb = new StringBuilder(type);
        if (name.Length > 0) sb.Append($" \"{Escape(name)}\"");
        if (id.Length > 0) sb.Append($" id={id}");
        if (cls.Length > 0) sb.Append($" class={cls}");
        if (!r.IsEmpty) sb.Append($" [{r.X - origin.X},{r.Y - origin.Y},{r.Width},{r.Height}]");
        if (!enabled) sb.Append(" disabled");
        if (offscreen) sb.Append(" offscreen");

        string? value = TryValue(e);
        if (!string.IsNullOrEmpty(value) && value != name) sb.Append($" value=\"{Escape(value)}\"");

        string? state = TryToggle(e);
        if (state is not null) sb.Append($" toggle={state}");

        return sb.ToString();
    }

    private static string? TryValue(AutomationElement e)
    {
        try
        {
            return e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryToggle(AutomationElement e)
    {
        try
        {
            var t = e.Patterns.Toggle.PatternOrDefault;
            if (t is not null) return t.ToggleState.ValueOrDefault.ToString();
            var s = e.Patterns.SelectionItem.PatternOrDefault;
            if (s is not null && e.ControlType == ControlType.RadioButton) return s.IsSelected.ValueOrDefault ? "On" : "Off";
        }
        catch (Exception)
        {
        }
        return null;
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
}
