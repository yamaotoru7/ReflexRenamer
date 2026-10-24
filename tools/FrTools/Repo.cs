namespace FrTools;

internal static class Repo
{
    /// <summary>FlexibleRenamer.slnx があるディレクトリ。</summary>
    public static string Root { get; } = Find();

    private static string Find()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "FlexibleRenamer.slnx")))
                {
                    return dir.FullName;
                }
            }
        }
        throw new DirectoryNotFoundException("FlexibleRenamer.slnx が見つからない");
    }
}
