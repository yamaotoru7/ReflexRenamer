using System.Globalization;
using FlexibleRenamer.Core.Ini;

namespace FlexibleRenamer.Core.Settings;

/// <summary>
/// 実行ディレクトリの <c>Flexible Renamer.ini</c>。セクションとキーはオリジナル版と同じ名前を使い、
/// 読めないキーは既定値にする。入力欄の履歴（MRU）は「セクションに MRU= を並べる」形式。
/// </summary>
public sealed class AppSettings
{
    public const string FileName = "Flexible Renamer.ini";

    /// <summary>履歴に残す件数（オリジナル版の実測では 10 件程度）。</summary>
    public const int MruLimit = 10;

    private readonly IniDocument _ini;

    private AppSettings(IniDocument ini, string path)
    {
        _ini = ini;
        Path = path;
    }

    public string Path { get; }

    public static AppSettings Load(string path) =>
        new(File.Exists(path) ? IniDocument.Load(path) : new IniDocument(), path);

    public void Save() => _ini.Save(Path);

    public string? Get(string section, string key) => _ini[section]?[key];

    public string Get(string section, string key, string fallback) => Get(section, key) ?? fallback;

    public int GetInt(string section, string key, int fallback) =>
        int.TryParse(Get(section, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public bool GetBool(string section, string key, bool fallback) => GetInt(section, key, fallback ? 1 : 0) != 0;

    public void Set(string section, string key, string value) => _ini.GetOrAdd(section).Set(key, value);

    public void Set(string section, string key, int value) => Set(section, key, value.ToString(CultureInfo.InvariantCulture));

    public void Set(string section, string key, bool value) => Set(section, key, value ? 1 : 0);

    /// <summary>入力欄の履歴。セクション名は欄ごと（<c>RenameFilter</c>、<c>WildCardSearch</c> など）。</summary>
    public IReadOnlyList<string> GetMru(string section) => _ini[section]?.GetAll("MRU").ToList() ?? [];

    /// <summary>履歴の先頭に値を入れる（既にあれば先頭に移す）。</summary>
    public void PushMru(string section, string value)
    {
        if (value.Length == 0)
        {
            return;
        }
        var list = GetMru(section).Where(v => !string.Equals(v, value, StringComparison.Ordinal)).ToList();
        list.Insert(0, value);
        _ini.GetOrAdd(section).SetAll("MRU", list.Take(MruLimit));
    }

    /// <summary>同じキーが並ぶ一覧（<c>[Preset]</c> の <c>Preset=</c> など）。</summary>
    public IReadOnlyList<string> GetList(string section, string key) => _ini[section]?.GetAll(key).ToList() ?? [];

    public void SetList(string section, string key, IEnumerable<string> values) => _ini.GetOrAdd(section).SetAll(key, values);
}
