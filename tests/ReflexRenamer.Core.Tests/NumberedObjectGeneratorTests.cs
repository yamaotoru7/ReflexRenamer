using ReflexRenamer.Core.NumberedObjects;

namespace ReflexRenamer.Core.Tests;

public sealed class NumberedObjectGeneratorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ReflexRenamerTests", Guid.NewGuid().ToString("N"));

    public NumberedObjectGeneratorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void NamesUseQuestionMarksAsNumber()
    {
        var names = NumberedObjectGenerator.Names(new NumberedObjectOptions { Name = "file??.txt", Amount = 3, Start = 5, Step = 2 });
        Assert.Equal(["file05.txt", "file07.txt", "file09.txt"], names);
    }

    [Fact]
    public void NameWithoutQuestionMarkGetsNumberAppended()
    {
        var names = NumberedObjectGenerator.Names(new NumberedObjectOptions { Name = "", Amount = 3 });
        Assert.Equal(["0", "1", "2"], names);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1K", 1024)]
    [InlineData("2m", 2 * 1024 * 1024)]
    [InlineData("abc", 0)]
    public void ParsesSizeSuffixes(string text, long expected) => Assert.Equal(expected, NumberedObjectGenerator.ParseSize(text));

    [Fact]
    public void CreatesEmptyFilesAndFolders()
    {
        Assert.Equal(0, NumberedObjectGenerator.Create(_dir, new NumberedObjectOptions { Name = "f?.bin", Amount = 2, Size = "1K" }));
        Assert.Equal(1024, new FileInfo(Path.Combine(_dir, "f0.bin")).Length);

        Assert.Equal(0, NumberedObjectGenerator.Create(_dir, new NumberedObjectOptions { Kind = NumberedObjectKind.EmptyFolder, Name = "d?", Amount = 2 }));
        Assert.True(Directory.Exists(Path.Combine(_dir, "d1")));

        // 既にあるものは失敗として数える
        Assert.Equal(2, NumberedObjectGenerator.Create(_dir, new NumberedObjectOptions { Name = "f?.bin", Amount = 2 }));
    }
}
