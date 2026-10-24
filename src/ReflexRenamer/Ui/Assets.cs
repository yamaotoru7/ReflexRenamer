using System.Reflection;

namespace ReflexRenamer.Ui;

/// <summary>
/// オリジナル版の exe から抽出したアイコンとビットマップ（Assets/、FrTools res-dump で生成）。
/// アイコンはリソース ID（多くはコマンド ID と同じ）で引く。
/// </summary>
public static class Assets
{
    private static readonly Dictionary<int, Icon?> Icons = [];

    private static Icon? _appIcon;

    /// <summary>Reflex Renamer のアプリアイコン（Assets/Icons/app.ico。16〜256px）。オリジナル版の 64 ではない。</summary>
    public static Icon? AppIcon(int size = 0)
    {
        if (_appIcon is null)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Icons/app.ico");
            _appIcon = stream is null ? null : new Icon(stream);
        }
        return _appIcon is null || size == 0 ? _appIcon : new Icon(_appIcon, size, size);
    }

    public static Icon? Icon(int id, int size = 0)
    {
        if (!Icons.TryGetValue(id, out var icon))
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Icons/{id}.ico");
            icon = stream is null ? null : new Icon(stream);
            Icons[id] = icon;
        }
        return icon is null || size == 0 ? icon : new Icon(icon, size, size);
    }

    public static Bitmap? Bitmap(int id)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Bitmaps/{id}.bmp");
        return stream is null ? null : new Bitmap(stream);
    }
}
