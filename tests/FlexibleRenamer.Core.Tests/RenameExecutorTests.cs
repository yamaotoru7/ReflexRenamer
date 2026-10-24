using FlexibleRenamer.Core.Presets;
using FlexibleRenamer.Core.Rename;

namespace FlexibleRenamer.Core.Tests;

public sealed class RenameExecutorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FlexibleRenamerTests", Guid.NewGuid().ToString("N"), "work");

    public RenameExecutorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_dir)!, recursive: true);

    private void Create(params string[] names)
    {
        foreach (var n in names)
        {
            File.WriteAllText(Path.Combine(_dir, n), n);
        }
    }

    private List<PreviewRow> Preview(string search, string replace, RenameMode mode = RenameMode.Wildcard) =>
        new RenameEngine(new RenameOptions { Mode = mode, Search = search, Replace = replace })
            .Preview(FileCollector.Collect(_dir, new CollectOptions()));

    private string[] Names() => Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    [Fact]
    public void RenamesAndWritesLog()
    {
        Create("a.txt", "b.txt");
        string logPath = Path.Combine(Path.GetDirectoryName(_dir)!, "Rename.log");

        using (var log = RenameLog.Begin(logPath, "説明", new DateTime(2026, 1, 2, 3, 4, 5), _dir, "*.txt", "x_\\1.txt"))
        {
            var result = new RenameExecutor(new Callbacks()).Execute(Preview("*.txt", "x_\\1.txt"), log);
            Assert.Equal(new ExecuteResult(2, 0, false), result);
        }

        Assert.Equal(["x_a.txt", "x_b.txt"], Names());
        var entry = RenameLog.Read(logPath)!;
        Assert.True(entry.Completed);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), entry.Time);
        Assert.Equal("*.txt", entry.Search);
        Assert.Equal(2, entry.Pairs.Count);

        Assert.Equal(0, RenameExecutor.Undo(entry));
        Assert.Equal(["a.txt", "b.txt"], Names());
    }

    [Fact]
    public void CaseOnlyRenameIsNotConflict()
    {
        Create("a.txt");
        var result = new RenameExecutor(new Callbacks()).Execute(Preview("*", "\\U\\0"), null);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(["A.TXT"], Names());
    }

    [Fact]
    public void ConflictCanBeSkippedOrAutoNumbered()
    {
        Create("a.txt", "b.txt", "c.txt");
        var callbacks = new Callbacks { Conflict = new ConflictDecision(ConflictAction.Skip) };
        var result = new RenameExecutor(callbacks).Execute(Preview("[ab]", "c", RenameMode.RegularExpression), null);
        Assert.Equal(new ExecuteResult(0, 2, false), result);
        Assert.Equal(2, callbacks.ConflictCount);

        result = new RenameExecutor(new Callbacks()) { AlwaysAutoNumber = new AutoNumberSettings("(??)", AtEnd: true, Start: 1, Step: 1) }
            .Execute(Preview("[ab]", "c", RenameMode.RegularExpression), null);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(["c(01).txt", "c(02).txt", "c.txt"], Names());
    }

    [Fact]
    public void SlashMovesIntoSubfolder()
    {
        Create("a.txt");
        new RenameExecutor(new Callbacks()).Execute(Preview("*", "sub/\\0"), null);
        Assert.True(File.Exists(Path.Combine(_dir, "sub", "a.txt")));
    }

    [Fact]
    public void CopyToFolderKeepsSource()
    {
        Create("a.txt");
        string dest = Path.Combine(Path.GetDirectoryName(_dir)!, "dest");
        new RenameExecutor(new Callbacks()) { Operation = RenameOperation.CopyToFolder, DestinationFolder = dest }
            .Execute(Preview("*", "copy_\\0"), null);
        Assert.True(File.Exists(Path.Combine(_dir, "a.txt")));
        Assert.True(File.Exists(Path.Combine(dest, "copy_a.txt")));
    }

    [Fact]
    public void LogWithoutMarkerIsNotCompleted()
    {
        string logPath = Path.Combine(Path.GetDirectoryName(_dir)!, "Rename.log");
        using (var log = RenameLog.Begin(logPath, "説明", DateTime.Now, @"C:\a,b", "s", "r"))
        {
            RenameLog.WritePair(log, @"C:\a,b\x,1.txt", @"C:\a,b\y.txt");
        }
        var entry = RenameLog.Read(logPath)!;
        Assert.False(entry.Completed);
        Assert.Equal((@"C:\a,b\x,1.txt", @"C:\a,b\y.txt"), entry.Pairs[0]);
    }

    private sealed class Callbacks : IRenameCallbacks
    {
        public ConflictDecision Conflict { get; init; } = new(ConflictAction.Cancel);

        public int ConflictCount { get; private set; }

        public ConflictDecision OnConflict(string folder, string sourceName, string destinationName)
        {
            ConflictCount++;
            return Conflict;
        }

        public ErrorAction OnError(string folder, string sourceName, string destinationName, Exception error) => ErrorAction.Cancel;
    }
}
