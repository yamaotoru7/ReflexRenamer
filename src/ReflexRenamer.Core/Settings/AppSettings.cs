using System.Globalization;
using ReflexRenamer.Core.Ini;

namespace ReflexRenamer.Core.Settings;

/// <summary>
/// 実行ディレクトリの <c>Reflex Renamer.ini</c>。セクションとキーはオリジナル版と同じ名前を使い
/// （アプリ全体の設定だけは <c>[General]</c>。オリジナル版の <c>[Flexible Renamer]</c> から読み替える）、
/// 読めないキーは既定値にする。入力欄の履歴（MRU）は「セクションに MRU= を並べる」形式。
/// </summary>
public sealed class AppSettings
{
    public const string FileName = "Reflex Renamer.ini";

    /// <summary>履歴に残す件数（オリジナル版の実測では 10 件程度）。</summary>
    public const int MruLimit = 10;

    /// <summary>アプリ全体の設定（フォルダ、ページ、フォント、言語、配色など）のセクション。</summary>
    public const string General = "General";

    /// <summary>オリジナル版と 0.1.0 までの移植版で <see cref="General"/> の代わりに使っていたセクション。</summary>
    private const string LegacyGeneral = "Flexible Renamer";

    private readonly IniDocument _ini;

    private AppSettings(IniDocument ini, string path)
    {
        _ini = ini;
        Path = path;
        MigrateLegacyGeneral();
    }

    /// <summary>[Flexible Renamer] の値を [General] に移す（[General] に同じキーがあればそちらを優先）。</summary>
    private void MigrateLegacyGeneral()
    {
        var legacy = _ini[LegacyGeneral];
        if (legacy is null)
        {
            return;
        }
        var general = _ini.GetOrAdd(General);
        foreach (var (key, value) in legacy.Entries)
        {
            if (general[key] is null)
            {
                general.Set(key, value);
            }
        }
        _ini.Remove(LegacyGeneral);
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
