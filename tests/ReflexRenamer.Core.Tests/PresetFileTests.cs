using ReflexRenamer.Core.Presets;

namespace ReflexRenamer.Core.Tests;

public class PresetFileTests
{
    private static readonly PresetFile Reference = PresetFile.Load(ReferenceFiles.Get("Preset.ini"));

    [Fact]
    public void LoadsWildcardPreset()
    {
        var preset = Reference.Presets[0];

        Assert.Equal(RenameMode.Wildcard, preset.Mode);
        Assert.Equal("", preset.Script);
        Assert.Equal("*.*", preset.Search);
        Assert.Equal(@"\Y\m\d\H\M\S.\2", preset.Replace);
        Assert.Equal("DateTime", preset.Name);
    }

    [Fact]
    public void LoadsAllModes()
    {
        Assert.Contains(Reference.Presets, p => p.Mode == RenameMode.RegularExpression && p.Name == "Lower-Case Extension");
        Assert.Contains(Reference.Presets, p => p.Mode == RenameMode.CharacterRange && p.Name == "Upper-Case");
    }

    [Fact]
    public void LoadsTypicalWithHexParam()
    {
        var typical = Reference.Typicals[0];

        Assert.Equal(0x03, typical.Param);
        Assert.Equal(1, typical.Category);
        Assert.Equal(RenameMode.RegularExpression, typical.Mode);
        Assert.Equal(@"(.*?)(\.[^.]*)?$", typical.Search);
        Assert.Equal(@"\ADD\NUM\2", typical.Replace);
    }

    [Fact]
    public void LoadsTypicalWithScript()
    {
        Assert.Contains(Reference.Typicals, t => t.Script == "VBScript" && t.Replace.StartsWith("toFullKana("));
    }

    [Fact]
    public void LoadsTimestampFormats()
    {
        Assert.Equal(@"\Y\m\d", Reference.TimestampFormats[0]);
        Assert.Contains(@"\y\m\d\p\I\M\S", Reference.TimestampFormats);
    }
}
