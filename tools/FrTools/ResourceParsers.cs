using System.Text;

namespace FrTools;

internal sealed record Dialog(string Title, int Width, int Height, uint Style, string Font, List<DialogControl> Controls);

internal sealed record DialogControl(int Id, string Class, string Text, int X, int Y, int Width, int Height, uint Style);

internal sealed record MenuItem(int Id, string Text, bool Separator, List<MenuItem>? Children);

/// <summary>リソース内の構造体を順に読むための小さなリーダー。</summary>
internal sealed class ResReader(byte[] data)
{
    public int Pos { get; set; }

    public ushort U16() { var v = BitConverter.ToUInt16(data, Pos); Pos += 2; return v; }
    public short S16() { var v = BitConverter.ToInt16(data, Pos); Pos += 2; return v; }
    public uint U32() { var v = BitConverter.ToUInt32(data, Pos); Pos += 4; return v; }
    public byte U8() => data[Pos++];
    public bool AtEnd => Pos >= data.Length;

    public string Sz()
    {
        var sb = new StringBuilder();
        for (char c; (c = (char)U16()) != 0;)
        {
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>sz_Or_Ord：0 なら空、0xFFFF なら序数、それ以外は文字列。</summary>
    public string SzOrOrd(Func<int, string>? ordinal = null)
    {
        ushort first = BitConverter.ToUInt16(data, Pos);
        if (first == 0) { Pos += 2; return ""; }
        if (first == 0xFFFF) { Pos += 2; int o = U16(); return ordinal?.Invoke(o) ?? $"#{o}"; }
        return Sz();
    }

    public void Align4() => Pos = (Pos + 3) & ~3;
}

internal static class DialogParser
{
    private const uint DsSetFont = 0x40;

    public static Dialog Parse(byte[] b)
    {
        var r = new ResReader(b);
        bool ex = BitConverter.ToUInt16(b, 2) == 0xFFFF;
        uint style;
        int count;
        if (ex)
        {
            r.U16(); r.U16(); r.U32(); r.U32(); // dlgVer, signature, helpID, exStyle
            style = r.U32();
        }
        else
        {
            style = r.U32(); r.U32();
        }
        count = r.U16();
        r.S16(); r.S16();
        int cx = r.S16(), cy = r.S16();
        r.SzOrOrd(); r.SzOrOrd(); // menu, class
        string title = r.Sz();
        string font = "";
        if ((style & DsSetFont) != 0)
        {
            int pt = r.U16();
            if (ex) { r.U16(); r.U8(); r.U8(); }
            font = $"{r.Sz()} {pt}pt";
        }

        var controls = new List<DialogControl>();
        for (int i = 0; i < count; i++)
        {
            r.Align4();
            uint cstyle;
            int id;
            if (ex)
            {
                r.U32(); r.U32(); cstyle = r.U32();
            }
            else
            {
                cstyle = r.U32(); r.U32();
            }
            int x = r.S16(), y = r.S16(), w = r.S16(), h = r.S16();
            id = ex ? (int)r.U32() : r.U16();
            string cls = r.SzOrOrd(ClassName);
            string text = r.SzOrOrd();
            int extra = r.U16();
            r.Pos += extra;
            controls.Add(new DialogControl(id == 0xFFFF || id == -1 ? -1 : id, Describe(cls, cstyle), text, x, y, w, h, cstyle));
        }
        return new Dialog(title, cx, cy, style, font, controls);
    }

    private static string ClassName(int ordinal) => ordinal switch
    {
        0x80 => "Button",
        0x81 => "Edit",
        0x82 => "Static",
        0x83 => "ListBox",
        0x84 => "ScrollBar",
        0x85 => "ComboBox",
        _ => $"#{ordinal}",
    };

    /// <summary>Button はスタイルから種類（チェックボックス等）を補足する。</summary>
    private static string Describe(string cls, uint style)
    {
        if (cls != "Button") return cls;
        return (style & 0xF) switch
        {
            0x0 => "Button",
            0x1 => "Button(Default)",
            0x2 or 0x3 => "CheckBox",
            0x5 or 0x6 => "CheckBox(3state)",
            0x4 or 0x9 => "RadioButton",
            0x7 => "GroupBox",
            0xB => "Button(OwnerDraw)",
            _ => $"Button(0x{style & 0xF:X})",
        };
    }
}

internal static class MenuParser
{
    private const int MfPopup = 0x10;
    private const int MfEnd = 0x80;
    private const int MfSeparator = 0x800;

    public static List<MenuItem> Parse(byte[] b)
    {
        var r = new ResReader(b);
        int version = r.U16();
        int offset = r.U16();
        if (version == 1)
        {
            r.Pos += offset;
            return ParseEx(r);
        }
        return ParseStd(r);
    }

    private static List<MenuItem> ParseStd(ResReader r)
    {
        var items = new List<MenuItem>();
        while (!r.AtEnd)
        {
            int flags = r.U16();
            if ((flags & MfPopup) != 0)
            {
                string text = r.Sz();
                items.Add(new MenuItem(0, text, false, ParseStd(r)));
            }
            else
            {
                int id = r.U16();
                string text = r.Sz();
                bool sep = id == 0 && text.Length == 0 || (flags & MfSeparator) != 0;
                items.Add(new MenuItem(id, text, sep, null));
            }
            if ((flags & MfEnd) != 0) break;
        }
        return items;
    }

    private static List<MenuItem> ParseEx(ResReader r)
    {
        var items = new List<MenuItem>();
        while (!r.AtEnd)
        {
            r.Align4();
            uint type = r.U32();
            r.U32(); // state
            int id = (int)r.U32();
            int resInfo = r.U16();
            string text = r.Sz();
            List<MenuItem>? children = null;
            if ((resInfo & 0x01) != 0)
            {
                r.Align4();
                r.U32(); // helpID
                children = ParseEx(r);
            }
            items.Add(new MenuItem(id, text, (type & MfSeparator) != 0, children));
            if ((resInfo & MfEnd) != 0) break;
        }
        return items;
    }
}
