using ReflexRenamer.Core.Localization;
using ReflexRenamer.Core.Presets;
using ReflexRenamer.Core.Settings;

namespace ReflexRenamer.Core.Tests;

public sealed class PresetStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ReflexRenamerTests", Guid.NewGuid().ToString("N"));

    public PresetStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ImportsFromPresetIniWithTranslationAndSavesToSettings()
    {
        var settings = AppSettings.Load(Path.Combine(_dir, AppSettings.FileName));
        var strings = LanguageStrings.Load(ReferenceFiles.Get("Flexible Renamer.JA.Strings.ini"));
        var store = PresetStore.Load(settings, PresetFile.Load(ReferenceFiles.Get("Preset.ini")), strings);

        // 名前は訳語、%%Shortcut to %% は展開される（オリジナル版の ini と同じ）
        Assert.Equal("日時", store.Presets[0].Name);
        Assert.Contains(store.Presets, p => p.Search == "(.*)へのショートカット(.*)?" && p.Name == "\" へのショートカット\"を削除");

        store.Add(new Preset(RenameMode.Wildcard, "", "*.txt", "\\1.md", "テキスト"));
        store.Save(settings);
        settings.Save();

        var again = PresetStore.Load(AppSettings.Load(settings.Path), PresetFile.Load(ReferenceFiles.Get("Preset.ini")), strings);
        Assert.Equal(store.Presets.Count, again.Presets.Count);
        Assert.Equal("テキスト", again.Presets[^1].Name);
        Assert.Equal("\\1.md", again.Presets[^1].Replace);
    }

    [Fact]
    public void ReadsPresetsFromOriginalIni()
    {
        var settings = AppSettings.Load(ReferenceFiles.Get("Flexible Renamer.ini"));
        var store = PresetStore.Load(settings, PresetFile.Load(ReferenceFiles.Get("Preset.ini")), LanguageStrings.Load(ReferenceFiles.Get("Flexible Renamer.JA.Strings.ini")));
        Assert.Equal(22, store.Presets.Count);
        Assert.Equal("日時", store.Presets[0].Name);
    }
}
