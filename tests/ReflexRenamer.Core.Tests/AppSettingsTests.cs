using System.Text;
using ReflexRenamer.Core.Ini;
using ReflexRenamer.Core.Settings;

namespace ReflexRenamer.Core.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ReflexRenamerTests", Guid.NewGuid().ToString("N"));

    public AppSettingsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ReadsOriginalIni()
    {
        var s = AppSettings.Load(ReferenceFiles.Get("Flexible Renamer.ini"));
        // オリジナル版の [Flexible Renamer] は [General] として読む
        Assert.Equal(2, s.GetInt(AppSettings.General, "Page", 0));
        Assert.Equal("Tahoma", s.Get(AppSettings.General, "FontFace"));
        Assert.Null(s.Get("Flexible Renamer", "Page"));
        Assert.Equal(@"\Y\m\d\H\M\S", s.Get("Rename", "Typical_Timestamp"));
        Assert.Equal("*", s.GetMru("RenameFilter")[0]);
        Assert.Equal(22, s.GetList("Preset", "Preset").Count);
    }

    [Fact]
    public void MovesLegacySectionToGeneral()
    {
        string path = Path.Combine(_dir, AppSettings.FileName);
        File.WriteAllText(path, "[General]\r\nPage=1\r\n[Flexible Renamer]\r\nPage=3\r\nLanguage=EN\r\n", Encoding.Unicode);

        var s = AppSettings.Load(path);
        s.Save();

        // 同じキーは [General] を優先し、[Flexible Renamer] は消える
        var again = AppSettings.Load(path);
        Assert.Equal(1, again.GetInt(AppSettings.General, "Page", 0));
        Assert.Equal("EN", again.Get(AppSettings.General, "Language"));
        Assert.DoesNotContain("[Flexible Renamer]", File.ReadAllText(path, Encoding.Unicode));
    }

    [Fact]
    public void SavesAsUtf16AndRoundTrips()
    {
        string path = Path.Combine(_dir, AppSettings.FileName);
        var s = AppSettings.Load(path);
        s.Set(AppSettings.General, "Page", 3);
        s.Set("Rename", "Typical_Add", "値");
        s.PushMru("RenameFilter", "*.jpg");
        s.PushMru("RenameFilter", "*.txt");
        s.PushMru("RenameFilter", "*.jpg");
        s.Save();

        var bytes = File.ReadAllBytes(path);
        Assert.Equal([0xFF, 0xFE], bytes[..2]);
        Assert.Contains("\r\n", File.ReadAllText(path, Encoding.Unicode));

        var again = AppSettings.Load(path);
        Assert.Equal(3, again.GetInt(AppSettings.General, "Page", 0));
        Assert.Equal("値", again.Get("Rename", "Typical_Add"));
        Assert.Equal(["*.jpg", "*.txt"], again.GetMru("RenameFilter"));
    }

    [Fact]
    public void SetAllKeepsPositionOfFirstOccurrence()
    {
        var ini = IniDocument.Parse(new StringReader("[S]\na=1\nMRU=x\nMRU=y\nb=2\n"));
        ini["S"]!.SetAll("MRU", ["p", "q", "r"]);
        Assert.Equal(["a", "MRU", "MRU", "MRU", "b"], ini["S"]!.Entries.Select(e => e.Key));
    }

    [Fact]
    public void DestinationHistoryIsStoredOldestFirstAndListedNewestFirst()
    {
        string path = Path.Combine(_dir, AppSettings.FileName);
        var s = AppSettings.Load(path);
        Assert.Empty(DestinationHistory.Get(s));
        Assert.True(DestinationHistory.CopyFolderStructure(s));
        foreach (var d in new[] { @"C:", @"C:", @"C:\c", @"C:\d", @"C:\e", @"C:", @"c:\B" })
        {
            DestinationHistory.Push(s, d);
        }
        // 5件まで。同じフォルダ（大文字小文字は区別しない）は一番新しい位置に移る
        Assert.Equal([@"c:\B", @"C:", @"C:\e", @"C:\d", @"C:\c"], DestinationHistory.Get(s));
        Assert.Equal([@"C:\c", @"C:\d", @"C:\e", @"C:", @"c:\B"], s.GetList(DestinationHistory.Section, "Destination"));
    }
}
