using System.Globalization;
using System.Reflection;
using ReflexRenamer.Core.Localization;
using ReflexRenamer.Core.Resources;
using ReflexRenamer.Core.Settings;

namespace ReflexRenamer.Ui;

/// <summary>
/// アプリ全体で使うリソース（ダイアログ・メニューの定義、言語ファイル、フォント）。
/// データファイルはオリジナル版と同じく exe と同じフォルダに置く。
/// </summary>
public sealed class AppResources
{
    private AppResources(ResourceSet resources, LanguageStrings strings, string baseDir, AppSettings settings)
    {
        Resources = resources;
        Strings = strings;
        BaseDirectory = baseDir;
        Builder = new DialogBuilder(strings, UiFont);
        Settings = settings;
    }

    private static Font _uiFont = SystemFonts.MessageBoxFont ?? new Font("Tahoma", 9f);

    public static string ProductName => "Reflex Renamer";

    /// <summary>画面に出す版（csproj の Version。ビルドメタデータの + 以降は出さない）。</summary>
    public static string DisplayVersion { get; } =
        typeof(AppResources).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] is { Length: > 0 } v
            ? v
            : "0.1.0";

    /// <summary>実行ディレクトリの Reflex Renamer.ini。終了時に保存する。</summary>
    public AppSettings Settings { get; }

    public ResourceSet Resources { get; }

    public LanguageStrings Strings { get; }

    public string BaseDirectory { get; }

    public DialogBuilder Builder { get; }

    /// <summary>既定フォント（システムのメッセージボックス用フォント）。</summary>
    public static Font UiFont
    {
        get => _uiFont;
        set
        {
            _uiFont = value;
            DialogBuilder.UpdateBaseUnits(value);
        }
    }

    /// <summary>
    /// 言語ファイル（オリジナル版の配布物）にない、移植版で足した文言の訳。キーは英語の原文。
    /// 言語ファイルに訳がなく、ここにもなければ英語のまま出る。
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> ExtraStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JA"] = new()
        {
            ["Color &mode"] = "配色(&M)",
            ["Follow &system"] = "システムに従う(&S)",
            ["&Light"] = "ライト(&L)",
            ["&Dark"] = "ダーク(&D)",
            ["Changing the color mode is applied at the next starts."] = "配色の変更は次回の起動から反映されます。",
            ["built in"] = "内蔵済み",
        },
    };

    public string T(string key)
    {
        string translated = Strings.Translate(key);
        if (translated == key && ExtraStrings.TryGetValue(Strings.ShortName, out var extra) && extra.TryGetValue(key, out var v))
        {
            return v;
        }
        return translated;
    }

    /// <summary>ini の [Flexible Renamer] ColorMode。System（既定）／Light／Dark。</summary>
    public const string ColorModeKey = "ColorMode";

    public SystemColorMode ColorMode => Settings.Get("Flexible Renamer", ColorModeKey) switch
    {
        "Light" => SystemColorMode.Classic,
        "Dark" => SystemColorMode.Dark,
        _ => SystemColorMode.System,
    };

    /// <summary>配色の設定を保存する（反映は次回の起動から。SetColorMode はフォームを作る前にしか効かない）。</summary>
    public void SaveColorMode(SystemColorMode mode) =>
        Settings.Set("Flexible Renamer", ColorModeKey, mode switch
        {
            SystemColorMode.Classic => "Light",
            SystemColorMode.Dark => "Dark",
            _ => "System",
        });

    public static AppResources Load()
    {
        string baseDir = AppContext.BaseDirectory;
        using var json = Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.json")
            ?? throw new InvalidOperationException("resources.json が埋め込まれていない");
        var resources = ResourceSet.Load(json);
        var settings = AppSettings.Load(Path.Combine(baseDir, AppSettings.FileName));
        return new AppResources(resources, LanguageStrings.Load(FindStringsFile(baseDir, settings.Get("Flexible Renamer", "Language"))), baseDir, settings);
    }

    /// <summary>実行ディレクトリにある言語ファイルの一覧（コードと表示名）。</summary>
    public IEnumerable<(string Code, string Name)> AvailableLanguages()
    {
        foreach (var path in Directory.GetFiles(BaseDirectory, "Flexible Renamer.*.Strings.ini").Order())
        {
            string code = Path.GetFileName(path).Split('.')[1];
            string name;
            try
            {
                name = LanguageStrings.Load(path).Name;
            }
            catch (IOException)
            {
                continue;
            }
            yield return (code, name.Length > 0 ? name : code);
        }
    }

    /// <summary>設定で選んだ言語、なければ OS の表示言語に合う言語ファイル。なければ英語。</summary>
    private static string FindStringsFile(string baseDir, string? preferred)
    {
        var culture = CultureInfo.CurrentUICulture;
        foreach (var code in new[] { preferred ?? "", culture.Name.ToUpperInvariant(), culture.TwoLetterISOLanguageName.ToUpperInvariant(), "EN" })
        {
            if (code.Length == 0)
            {
                continue;
            }
            string path = Path.Combine(baseDir, $"Flexible Renamer.{code}.Strings.ini");
            if (File.Exists(path))
            {
                return path;
            }
        }
        throw new FileNotFoundException("言語ファイルが見つからない", Path.Combine(baseDir, "Flexible Renamer.EN.Strings.ini"));
    }
}
