namespace FlexibleRenamer.Core.Rename;

/// <summary>
/// リネームの対象になるファイルまたはフォルダ。
/// </summary>
public sealed record FileItem(
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTime Created,
    DateTime Modified,
    DateTime Accessed)
{
    public string Name => Path.GetFileName(FullPath);

    /// <summary>このファイルがあるフォルダのフルパス。</summary>
    public string DirectoryPath => Path.GetDirectoryName(FullPath) ?? "";

    /// <summary>拡張子を除いた名前（<c>\t</c>）。フォルダは名前全体。</summary>
    public string Title => IsDirectory ? Name : FileNameParts.Title(Name);

    /// <summary>ドット付きの拡張子（<c>\e</c>）。なければ空。フォルダは常に空。</summary>
    public string Extension => IsDirectory ? "" : FileNameParts.Extension(Name);

    public static FileItem FromPath(string path)
    {
        if (Directory.Exists(path))
        {
            var d = new DirectoryInfo(path);
            return new FileItem(d.FullName, true, 0, d.CreationTime, d.LastWriteTime, d.LastAccessTime);
        }
        var f = new FileInfo(path);
        return new FileItem(f.FullName, false, f.Length, f.CreationTime, f.LastWriteTime, f.LastAccessTime);
    }
}

/// <summary>名前をタイトルと拡張子に分ける。拡張子は最後のドットから後ろ。</summary>
public static class FileNameParts
{
    public static int ExtensionStart(string name) => name.LastIndexOf('.');

    public static string Title(string name)
    {
        int dot = ExtensionStart(name);
        return dot < 0 ? name : name[..dot];
    }

    public static string Extension(string name)
    {
        int dot = ExtensionStart(name);
        return dot < 0 ? "" : name[dot..];
    }
}
