using FlexibleRenamer.Core.Tags;

namespace FlexibleRenamer.Core.Tests;

public sealed class ShellColumnsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FlexibleRenamerTests", Guid.NewGuid().ToString("N"));

    public ShellColumnsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void BrokenMp3CannotOpenPropertyStoreAndBecomesUnknownTag()
    {
        // 壊れた mp3 はプロパティハンドラが失敗する → オリジナル版と同じく全部「UnknownTag」
        string broken = Path.Combine(_dir, "broken.mp3");
        File.WriteAllBytes(broken, [1, 2, 3, 4]);
        Assert.False(PropertyStoreReader.CanOpen(broken));

        var shell = ShellColumns.Create();
        Assert.NotNull(shell);
        Assert.Equal(ShellColumns.UnknownTag, shell.Value(broken, 21));
    }

    [Fact]
    public void PlainFileGivesNameColumn()
    {
        string text = Path.Combine(_dir, "note.txt");
        File.WriteAllText(text, "hello");
        Assert.True(PropertyStoreReader.CanOpen(text));

        var shell = ShellColumns.Create();
        Assert.NotNull(shell);
        // 0 番の列（名前）はファイル名そのもの
        Assert.Equal("note.txt", shell.Value(text, 0));
        // 21 番（タイトル）はテキストファイルにはない → 空
        Assert.Equal("", shell.Value(text, 21));
    }
}
