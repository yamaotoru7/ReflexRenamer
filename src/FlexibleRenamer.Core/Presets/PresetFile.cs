using System.Globalization;
using FlexibleRenamer.Core.Ini;

namespace FlexibleRenamer.Core.Presets;

/// <summary>
/// <c>Preset.ini</c> の内容。
/// </summary>
public sealed class PresetFile
{
    public IReadOnlyList<Preset> Presets { get; private init; } = [];

    public IReadOnlyList<TypicalPreset> Typicals { get; private init; } = [];

    /// <summary>日時の書式（<c>\Y\m\d</c> など）の候補。</summary>
    public IReadOnlyList<string> TimestampFormats { get; private init; } = [];

    public static PresetFile Load(string path) => FromIni(IniDocument.Load(path));

    public static PresetFile FromIni(IniDocument ini) => new()
    {
        Presets = Values(ini, "Preset", "Preset").Select(ParsePreset).ToList(),
        Typicals = Values(ini, "Typical", "Typical").Select(ParseTypical).ToList(),
        TimestampFormats = Values(ini, "Timestamp", "Format").ToList(),
    };

    private static IEnumerable<string> Values(IniDocument ini, string section, string key) =>
        ini[section]?.GetAll(key) ?? [];

    private static Preset ParsePreset(string value)
    {
        var f = Split(value, 5);
        return new Preset((RenameMode)int.Parse(f[0], CultureInfo.InvariantCulture), f[1], f[2], f[3], f[4]);
    }

    private static TypicalPreset ParseTypical(string value)
    {
        var f = Split(value, 7);
        return new TypicalPreset(
            ParseInt(f[0]),
            ParseInt(f[1]),
            (RenameMode)ParseInt(f[2]),
            f[3],
            f[4],
            f[5],
            f[6]);
    }

    /// <summary>
    /// タブで分割する。最後のフィールドには残りをすべて含め、足りないフィールドは空文字で埋める。
    /// </summary>
    private static string[] Split(string value, int count)
    {
        var parts = value.Split('\t', count);
        if (parts.Length < count)
        {
            Array.Resize(ref parts, count);
            for (int i = 0; i < count; i++)
            {
                parts[i] ??= "";
            }
        }
        return parts;
    }

    private static int ParseInt(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.Parse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : int.Parse(s, CultureInfo.InvariantCulture);
}
