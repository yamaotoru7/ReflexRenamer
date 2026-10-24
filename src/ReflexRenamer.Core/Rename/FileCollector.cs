using System.Text.RegularExpressions;

namespace ReflexRenamer.Core.Rename;

/// <summary>ファイル一覧を集めるときの条件（リネームページの「ターゲット」欄）。</summary>
public sealed record CollectOptions
{
    public bool Files { get; init; } = true;

    public bool Folders { get; init; }

    /// <summary>
    /// ワイルドカード。<c>;</c> で区切って複数指定でき、区切りごとに順に列挙する（重複も残す）。
    /// <c>.</c> を含まない語は拡張子とみなす（<c>jpg</c> は <c>*.jpg</c>）。サブフォルダをたどらないときはフォルダにも適用する。
    /// </summary>
    public string Filter { get; init; } = "*";

    public bool Recursive { get; init; }

    /// <summary>サブフォルダをたどる深さ。0 は無制限。</summary>
    public int Depth { get; init; }

    /// <summary>保護されたオペレーティング システム ファイル（隠し属性かつシステム属性）を表示しない。既定はオン。</summary>
    public bool HideProtectedOsFiles { get; init; } = true;
}

/// <summary>
/// 指定フォルダからリネーム対象の一覧を集める。並び順はオリジナル版の実測に合わせる。
/// <list type="bullet">
/// <item>サブフォルダをたどらないときは、フィルタの区切りごとに、フォルダとファイルを混ぜて NTFS の列挙順</item>
/// <item>たどるときは、各フォルダの中でサブフォルダ（フィルタに関係なくすべて）、フィルタの区切りごとのファイルの順に並べ、そのあとでサブフォルダの中へ深さ優先で潜る</item>
/// </list>
/// </summary>
public static class FileCollector
{
    public static List<FileItem> Collect(string folder, CollectOptions options)
    {
        var filters = ParseFilter(options.Filter);

        var result = new List<FileItem>();
        if (options.Recursive)
        {
            CollectRecursive(new DirectoryInfo(folder), options, filters, 1, result);
        }
        else
        {
            var entries = Entries(new DirectoryInfo(folder), options).ToList();
            foreach (var filter in filters)
            {
                foreach (var entry in entries)
                {
                    if ((entry is DirectoryInfo ? options.Folders : options.Files) && filter.IsMatch(entry.Name))
                    {
                        result.Add(ToItem(entry));
                    }
                }
            }
        }
        return result;
    }

    private static void CollectRecursive(DirectoryInfo dir, CollectOptions options, List<Regex> filters, int level, List<FileItem> result)
    {
        var entries = Entries(dir, options).ToList();
        var subdirs = entries.OfType<DirectoryInfo>().ToList();
        foreach (var d in subdirs)
        {
            if (options.Folders)
            {
                result.Add(ToItem(d));
            }
        }
        if (options.Files)
        {
            var files = entries.OfType<FileInfo>().ToList();
            foreach (var filter in filters)
            {
                foreach (var f in files)
                {
                    if (filter.IsMatch(f.Name))
                    {
                        result.Add(ToItem(f));
                    }
                }
            }
        }
        if (options.Depth > 0 && level > options.Depth)
        {
            return;
        }
        foreach (var d in subdirs)
        {
            CollectRecursive(d, options, filters, level + 1, result);
        }
    }

    private static IEnumerable<FileSystemInfo> Entries(DirectoryInfo dir, CollectOptions options)
    {
        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = dir.EnumerateFileSystemInfos().ToList();
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return [];
        }
        if (options.HideProtectedOsFiles)
        {
            const FileAttributes protectedOs = FileAttributes.Hidden | FileAttributes.System;
            entries = entries.Where(e => (e.Attributes & protectedOs) != protectedOs);
        }
        return entries.OrderBy(e => e.Name, FileOrder.Ntfs);
    }

    /// <summary>
    /// ファイル指定モード：ドロップされたパスの一覧から対象を作る。フォルダはサブフォルダ以下も対象のときだけ中に入る。
    /// </summary>
    public static List<FileItem> CollectSpecified(IEnumerable<string> paths, CollectOptions options)
    {
        var filters = ParseFilter(options.Filter);
        var result = new List<FileItem>();
        foreach (var path in paths)
        {
            FileSystemInfo entry = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            if (!entry.Exists)
            {
                continue;
            }
            if (Accepts(entry, options, filters))
            {
                result.Add(ToItem(entry));
            }
            if (entry is DirectoryInfo dir && options.Recursive)
            {
                CollectRecursive(dir, options, filters, 1, result);
            }
        }
        return result;
    }

    private static bool Accepts(FileSystemInfo entry, CollectOptions options, List<Regex> filters) =>
        entry is DirectoryInfo
            ? options.Folders
            : options.Files && filters.Any(f => f.IsMatch(entry.Name));

    /// <summary>
    /// フィルタ欄の文字列を区切りごとのパターンにする。空なら <c>*</c>。
    /// <c>.</c> を含まない語は、<c>*</c> 以外なら前に <c>*.</c> を付けて拡張子の指定にする（オリジナル版の実測：<c>a*</c> は <c>a.jpg</c> に一致しない）。
    /// </summary>
    public static List<Regex> ParseFilter(string filter)
    {
        var patterns = filter
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p == "*" || p.Contains('.') ? p : "*." + p)
            .ToList();
        if (patterns.Count == 0)
        {
            patterns.Add("*");
        }
        return patterns.Select(ToFindPattern).ToList();
    }

    /// <summary>
    /// FindFirstFile と同じく、末尾の <c>.*</c> は拡張子がない名前にも一致させる（<c>*.*</c> はすべての名前に一致する）。
    /// </summary>
    private static Regex ToFindPattern(string pattern)
    {
        if (!pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            return WildcardPattern.ToRegex(pattern);
        }
        var withExt = WildcardPattern.ToRegex(pattern);
        var withoutExt = WildcardPattern.ToRegex(pattern[..^2]);
        return new Regex($"(?:{withExt})|(?:{withoutExt})", withExt.Options);
    }

    private static FileItem ToItem(FileSystemInfo e) => e switch
    {
        FileInfo f => new FileItem(f.FullName, false, f.Length, f.CreationTime, f.LastWriteTime, f.LastAccessTime),
        _ => new FileItem(e.FullName, true, 0, e.CreationTime, e.LastWriteTime, e.LastAccessTime),
    };
}
