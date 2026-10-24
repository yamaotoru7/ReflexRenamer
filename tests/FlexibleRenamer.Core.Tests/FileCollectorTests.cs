using FlexibleRenamer.Core.Rename;

namespace FlexibleRenamer.Core.Tests;

public sealed class FileCollectorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FlexibleRenamerTests", Guid.NewGuid().ToString("N"));

    public FileCollectorTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "sub"));
        foreach (var n in new[] { "b.txt", "a.jpg", "sub/c.txt", "file10.txt", "file2.txt" })
        {
            File.WriteAllText(Path.Combine(_dir, n), n);
        }
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void SpecifiedPathsKeepOrderAndApplyFilter()
    {
        var items = FileCollector.CollectSpecified([Path.Combine(_dir, "b.txt"), Path.Combine(_dir, "a.jpg"), Path.Combine(_dir, "missing.txt")], new CollectOptions { Filter = "*.txt" });
        Assert.Equal(["b.txt"], items.Select(i => i.Name));
    }

    [Fact]
    public void SpecifiedFolderIsExpandedOnlyWhenRecursive()
    {
        string sub = Path.Combine(_dir, "sub");
        Assert.Empty(FileCollector.CollectSpecified([sub], new CollectOptions()));
        var items = FileCollector.CollectSpecified([sub], new CollectOptions { Recursive = true, Folders = true });
        Assert.Equal(["sub", "c.txt"], items.Select(i => i.Name));
    }

    [Fact]
    public void NaturalOrderComparesNumbersNumerically()
    {
        var names = new[] { "file10.txt", "file2.txt", "a.jpg" }.OrderBy(n => n, NaturalOrder.Instance).ToArray();
        Assert.Equal(["a.jpg", "file2.txt", "file10.txt"], names);
    }

    // 以下はオリジナル版の実測（compare explore-target-filter）に合わせたフィルタの規則

    [Theory]
    [InlineData("*", "a.jpg,b.txt,file10.txt,file2.txt,sub")]
    [InlineData("", "a.jpg,b.txt,file10.txt,file2.txt,sub")]
    [InlineData("*.*", "a.jpg,b.txt,file10.txt,file2.txt,sub")]
    [InlineData("jpg", "a.jpg")]
    [InlineData("txt;jpg", "b.txt,file10.txt,file2.txt,a.jpg")]
    [InlineData("*.TXT;*.txt", "b.txt,file10.txt,file2.txt,b.txt,file10.txt,file2.txt")]
    [InlineData("*;*.jpg", "a.jpg,b.txt,file10.txt,file2.txt,sub,a.jpg")]
    [InlineData("a*", "")]
    [InlineData("sub", "")]
    [InlineData("su*", "")]
    [InlineData("?.txt;", "b.txt")]
    public void FilterIsListedPerPatternAndAppliesToFolders(string filter, string expected)
    {
        var items = FileCollector.Collect(_dir, new CollectOptions { Folders = true, Filter = filter });
        Assert.Equal(expected, string.Join(",", items.Select(i => i.Name)));
    }

    [Fact]
    public void RecursiveListsAllFoldersAndFilesPerPattern()
    {
        var items = FileCollector.Collect(_dir, new CollectOptions { Folders = true, Recursive = true, Filter = "*.txt;*.jpg" });
        Assert.Equal("sub,b.txt,file10.txt,file2.txt,a.jpg,c.txt", string.Join(",", items.Select(i => i.Name)));
        var none = FileCollector.Collect(_dir, new CollectOptions { Folders = true, Recursive = true, Filter = "x*" });
        Assert.Equal("sub", string.Join(",", none.Select(i => i.Name)));
    }
}
