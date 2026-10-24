namespace FrTools;

internal static class Repo
{
    /// <summary>ReflexRenamer.slnx があるディレクトリ。</summary>
    public static string Root { get; } = Find();

    private static string Find()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ReflexRenamer.slnx")))
                {
                    return dir.FullName;
                }
            }
        }
        throw new DirectoryNotFoundException("ReflexRenamer.slnx が見つからない");
    }
}
