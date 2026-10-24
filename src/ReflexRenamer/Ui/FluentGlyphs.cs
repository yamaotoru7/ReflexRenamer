using System.Drawing.Text;

namespace ReflexRenamer.Ui;

/// <summary>
/// Segoe Fluent Icons（Windows 11）または Segoe MDL2 Assets（Windows 10）のグリフを描く。
/// どちらのフォントもない環境では、オリジナル版のアイコン（<see cref="Assets.Icon"/>）にフォールバックする。
/// </summary>
public static class FluentGlyphs
{
    public const char Rename = '\uE8AC';
    public const char Tag = '\uE8EC';
    public const char Attribute = '\uE946';
    public const char NumberedObject = '\uE8F4';
    public const char SpecifyFile = '\uE8A5';
    public const char SpecifyFolder = '\uE8B7';

    private static readonly string? FontName = FindFontName();

    private static string? FindFontName()
    {
        foreach (string name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            try
            {
                using var family = new FontFamily(name);
                if (string.Equals(family.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return family.Name;
                }
            }
            catch (ArgumentException)
            {
            }
        }
        return null;
    }

    /// <summary>グリフを指定サイズのビットマップに描く。</summary>
    public static Bitmap Render(char glyph, int size, Color color)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        Draw(g, glyph, new Rectangle(0, 0, size, size), color);
        return bmp;
    }

    /// <summary>グリフを指定の矩形の中央に描く。</summary>
    public static void Draw(Graphics g, char glyph, Rectangle bounds, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        if (FontName is not null)
        {
            int px = Math.Min(bounds.Width, bounds.Height);
            using var font = new Font(FontName, px, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(color);
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip,
            };
            var prevHint = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(glyph.ToString(), font, brush, bounds, format);
            g.TextRenderingHint = prevHint;
            return;
        }
        if (CommandOf(glyph) is { } command && Assets.Icon(command, Math.Min(bounds.Width, bounds.Height)) is { } icon)
        {
            g.DrawIcon(icon, bounds);
        }
    }

    private static int? CommandOf(char glyph) => glyph switch
    {
        Rename => Commands.PageRename,
        Tag => Commands.PageTagRename,
        Attribute or '\uE90F' => Commands.PageAttribute,
        NumberedObject => Commands.PageNumberedObject,
        SpecifyFile => Commands.SpecifyFile,
        SpecifyFolder => Commands.SpecifyFolder,
        _ => null,
    };
}
