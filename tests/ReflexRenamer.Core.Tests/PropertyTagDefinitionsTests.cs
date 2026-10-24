using ReflexRenamer.Core.Tags;

namespace ReflexRenamer.Core.Tests;

public class PropertyTagDefinitionsTests
{
    [Fact]
    public void LoadsLatestTableForWindows11()
    {
        // Windows 11（ビルド 26100）はどの表よりも新しいので、最大の 6110 を使う（オリジナル版の ini の OSVersion_PropertyTag=6110）
        var defs = PropertyTagDefinitions.Load(ReferenceFiles.Get("TagDef.ini"), 26100);
        Assert.Equal(6110, defs.Version);
        Assert.Equal(["Music", "Picture", "Image", "Video", "Document"], defs.Presets.Select(p => p.Name));
        Assert.Equal("mp3;wma", defs.Presets[0].Filter);
        Assert.Equal([13, 14, 15, 16, 21, 24, 26, 27, 28, 217, 220], defs.Presets[0].Columns);
        Assert.Equal(["mp3", "wma"], defs.Presets[0].Extensions);
        Assert.Equal(11, defs.Groups.Count);
        Assert.Equal("Description", defs.Groups[0].Name);
        Assert.True(defs.Groups[^1].IsOther);
    }

    [Fact]
    public void PicksTableAtOrBelowOsVersion()
    {
        Assert.Equal(6100, PropertyTagDefinitions.Load(ReferenceFiles.Get("TagDef.ini"), 6105).Version);
        Assert.Equal(5100, PropertyTagDefinitions.Load(ReferenceFiles.Get("TagDef.ini"), 5100).Version);
    }

    [Fact]
    public void RoundTripsPresetLines()
    {
        var presets = new List<PropertyPreset>
        {
            new("ミュージック", "mp3;wma", [13, 14]),
            new("写真", "jpg;jpe", [12]),
        };
        var lines = PropertyTagDefinitions.FormatPresets(presets).ToList();
        // オリジナル版の ini と同じく番号の大きい順に書く
        Assert.Equal("Preset=2\t\tjpg;jpe\t12\t写真", lines[0]);
        Assert.Equal("Preset=1\t\tmp3;wma\t13,14\tミュージック", lines[1]);
        var again = PropertyTagDefinitions.ParsePresets(lines);
        Assert.Equal(presets.Select(p => (p.Name, p.Filter, string.Join(',', p.Columns))), again.Select(p => (p.Name, p.Filter, string.Join(',', p.Columns))));
    }

    [Fact]
    public void ShellColumnNamesAreAvailable()
    {
        var shell = ShellColumns.Create();
        Assert.NotNull(shell);
        // 0 番は「名前」。日本語の Windows では「名前」、それ以外でも空ではない
        Assert.Equal(0, shell.Names[0].Index);
        Assert.NotEmpty(shell.Names[0].Name);
        Assert.Equal(0, shell.ColumnOf(shell.Names[0].Name));
    }
}
