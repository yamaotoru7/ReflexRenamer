using FlexibleRenamer.Core.Attributes;
using FlexibleRenamer.Core.Rename;

namespace FlexibleRenamer.Core.Tests;

public sealed class AttributeChangerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FlexibleRenamerTests", Guid.NewGuid().ToString("N"));
    private readonly DateTime _base = new(2020, 1, 2, 3, 4, 5);

    public AttributeChangerTests()
    {
        Directory.CreateDirectory(_dir);
        foreach (var n in new[] { "a.txt", "b.txt" })
        {
            string p = Path.Combine(_dir, n);
            File.WriteAllText(p, n);
            File.SetCreationTime(p, _base);
            File.SetLastWriteTime(p, _base.AddDays(1));
            File.SetLastAccessTime(p, _base.AddDays(2));
        }
    }

    public void Dispose()
    {
        foreach (var f in Directory.GetFiles(_dir))
        {
            File.SetAttributes(f, FileAttributes.Normal);
        }
        Directory.Delete(_dir, recursive: true);
    }

    private List<FileItem> Items() => FileCollector.Collect(_dir, new CollectOptions());

    [Fact]
    public void SpecifyChangesOnlyCheckedTimestamps()
    {
        var target = new DateTime(2001, 1, 1);
        var plans = AttributeChanger.Plan(Items(), new AttributeOptions { Mode = TimestampMode.Specify, Updated = target });
        Assert.Equal(0, AttributeChanger.Execute(plans));
        string p = Path.Combine(_dir, "a.txt");
        Assert.Equal(target, File.GetLastWriteTime(p));
        Assert.Equal(_base, File.GetCreationTime(p));
    }

    [Fact]
    public void ShiftEarlierByDaysAndTime()
    {
        var plans = AttributeChanger.Plan(Items(), new AttributeOptions
        {
            Mode = TimestampMode.Shift,
            ShiftEarlier = true,
            ShiftDays = 1,
            ShiftTime = TimeSpan.FromHours(2),
            ApplyToUpdated = true,
        });
        Assert.Equal(_base.AddDays(1) - TimeSpan.FromDays(1) - TimeSpan.FromHours(2), plans[0].Updated);
        Assert.Null(plans[0].Created);
    }

    [Fact]
    public void SequentialAdvancesPerFile()
    {
        var start = new DateTime(2010, 5, 5);
        var plans = AttributeChanger.Plan(Items(), new AttributeOptions
        {
            Mode = TimestampMode.Sequential,
            SequenceStart = start,
            SequenceStep = TimeSpan.FromMinutes(1),
            ApplyToUpdated = true,
        });
        Assert.Equal(start, plans[0].Updated);
        Assert.Equal(start.AddMinutes(1), plans[1].Updated);
    }

    [Fact]
    public void SequentialEarlierGoesBackward()
    {
        // オリジナル版の実測（exec-attribute-seq）：前に・2日で 10/23, 10/21, ...
        var start = new DateTime(2026, 10, 23);
        var plans = AttributeChanger.Plan(Items(), new AttributeOptions
        {
            Mode = TimestampMode.Sequential,
            SequenceStart = start,
            SequenceStep = TimeSpan.FromDays(2),
            ShiftEarlier = true,
            ApplyToUpdated = true,
        });
        Assert.Equal(start, plans[0].Updated);
        Assert.Equal(new DateTime(2026, 10, 21), plans[1].Updated);
    }

    [Fact]
    public void MakeSameCopiesCreatedToUpdated()
    {
        var plans = AttributeChanger.Plan(Items(), new AttributeOptions { Mode = TimestampMode.MakeSame, BaseKind = TimestampKind.Created, ApplyToUpdated = true });
        Assert.Equal(_base, plans[0].Updated);
    }

    [Fact]
    public void AttributesSetAndClear()
    {
        var plans = AttributeChanger.Plan(Items(), new AttributeOptions { ReadOnly = AttributeChange.Set, Archive = AttributeChange.Clear });
        AttributeChanger.Execute(plans);
        var a = File.GetAttributes(Path.Combine(_dir, "a.txt"));
        Assert.True((a & FileAttributes.ReadOnly) != 0);
        Assert.True((a & FileAttributes.Archive) == 0);

        // 読み取り専用でもタイムスタンプを変えられ、属性は保たれる
        var again = AttributeChanger.Plan(Items(), new AttributeOptions { Mode = TimestampMode.Specify, Updated = new DateTime(2002, 2, 2) });
        Assert.Equal(0, AttributeChanger.Execute(again));
        Assert.True((File.GetAttributes(Path.Combine(_dir, "a.txt")) & FileAttributes.ReadOnly) != 0);
    }
}
