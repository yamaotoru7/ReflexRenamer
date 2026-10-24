namespace FlexibleRenamer.Core.Tests;

/// <summary>
/// リポジトリ直下の <c>FlexRenamer/</c>（オリジナル版の配布物）にあるファイルの場所を返す。
/// </summary>
internal static class ReferenceFiles
{
    private static readonly Lazy<string> Repo = new(() =>
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FlexibleRenamer.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("FlexibleRenamer.slnx が見つからない");
    });

    /// <summary>FlexibleRenamer.slnx があるディレクトリ。</summary>
    public static string RepoRoot => Repo.Value;

    public static string Get(string fileName) => Path.Combine(Repo.Value, "FlexRenamer", fileName);
}
