using System.Runtime.InteropServices;

namespace FlexibleRenamer.Ui;

/// <summary>シェルから、ファイルの種類の表示名とアイコンを取る（SHGetFileInfo）。</summary>
public static class ShellInfo
{
    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiTypeName = 0x400;
    private const uint ShgfiSmallIcon = 0x1;
    private const uint ShgfiUseFileAttributes = 0x10;
    private const uint FileAttributeDirectory = 0x10;
    private const uint FileAttributeNormal = 0x80;

    private static readonly Dictionary<string, string> TypeNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>「TXT ファイル」のような種類名。拡張子ごとにキャッシュする。</summary>
    public static string TypeName(string path, bool isDirectory)
    {
        string key = isDirectory ? "<dir>" : Path.GetExtension(path);
        if (TypeNames.TryGetValue(key, out var name))
        {
            return name;
        }
        var info = new ShFileInfo();
        // オリジナル版は拡張子だけで種類を引くので、拡張子のないファイル（README）は空文字列の問い合わせになり「ローカル ディスク」と出る（explore-typical）
        SHGetFileInfo(isDirectory ? path : key, isDirectory ? FileAttributeDirectory : FileAttributeNormal, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiTypeName | ShgfiUseFileAttributes);
        TypeNames[key] = info.TypeName;
        return info.TypeName;
    }

    /// <summary>小さいアイコン。実ファイルから取るので exe や lnk は固有のアイコンになる。</summary>
    public static Icon? SmallIcon(string path)
    {
        var info = new ShFileInfo();
        if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiSmallIcon) == 0 || info.Icon == 0)
        {
            return null;
        }
        var icon = (Icon)Icon.FromHandle(info.Icon).Clone();
        DestroyIcon(info.Icon);
        return icon;
    }

    /// <summary>拡張子だけから決まる小さいアイコン（ファイルがなくてもよい）。</summary>
    public static Icon? SmallIconForType(string extension)
    {
        var info = new ShFileInfo();
        if (SHGetFileInfo("x" + extension, FileAttributeNormal, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiSmallIcon | ShgfiUseFileAttributes) == 0 || info.Icon == 0)
        {
            return null;
        }
        var icon = (Icon)Icon.FromHandle(info.Icon).Clone();
        DestroyIcon(info.Icon);
        return icon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(string path, uint attributes, ref ShFileInfo info, uint size, uint flags);

    [DllImport("user32")]
    private static extern bool DestroyIcon(nint icon);
}
