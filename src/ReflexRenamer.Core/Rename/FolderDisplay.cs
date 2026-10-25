namespace ReflexRenamer.Core.Rename;

/// <summary>
/// 一覧の「フォルダ」列の表示。開いているフォルダからの相対パスにする（移植版だけの変更）。
/// </summary>
public static class FolderDisplay
{
    /// <summary>
    /// <paramref name="folder"/> を <paramref name="baseFolder"/> からの相対パスにする。
    /// 同じフォルダなら「.」、配下でなければフルパスのまま。
    /// </summary>
    public static string Relative(string baseFolder, string folder)
    {
        if (string.IsNullOrEmpty(baseFolder))
        {
            return folder;
        }
        string relative = Path.GetRelativePath(baseFolder, folder);
        return Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? folder : relative;
    }
}
