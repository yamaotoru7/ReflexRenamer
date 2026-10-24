using System.Globalization;
using FlexibleRenamer.Core.Localization;
using FlexibleRenamer.Core.Settings;

namespace FlexibleRenamer.Core.Presets;

/// <summary>
/// ユーザーのプリセット。設定ファイルの <c>[Preset]</c> に Preset.ini と同じタブ区切りで持つ（名前は訳語、<c>%%Key%%</c> は展開済み）。
/// 設定ファイルにまだなければ Preset.ini から取り込む。
/// </summary>
public sealed class PresetStore
{
    private readonly List<Preset> _presets;

    private PresetStore(List<Preset> presets) => _presets = presets;

    public IReadOnlyList<Preset> Presets => _presets;

    public static PresetStore Load(AppSettings settings, PresetFile defaults, LanguageStrings strings)
    {
        var lines = settings.GetList("Preset", "Preset");
        if (lines.Count > 0)
        {
            return new PresetStore(lines.Select(Parse).Where(p => p is not null).Select(p => p!).ToList());
        }
        var imported = defaults.Presets
            .Select(p => new Preset(p.Mode, p.Script, strings.Expand(p.Search), strings.Expand(p.Replace), strings.TranslatePreset(p.Name)))
            .ToList();
        return new PresetStore(imported);
    }

    public void Save(AppSettings settings) =>
        settings.SetList("Preset", "Preset", _presets.Select(p => string.Join('\t', ((int)p.Mode).ToString(CultureInfo.InvariantCulture), p.Script, p.Search, p.Replace, p.Name)));

    public void Add(Preset preset) => _presets.Add(preset);

    public void Replace(IEnumerable<Preset> presets)
    {
        _presets.Clear();
        _presets.AddRange(presets);
    }

    private static Preset? Parse(string line)
    {
        var f = line.Split('\t', 5);
        if (f.Length < 5 || !int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mode))
        {
            return null;
        }
        return new Preset((RenameMode)mode, f[1], f[2], f[3], f[4]);
    }
}
