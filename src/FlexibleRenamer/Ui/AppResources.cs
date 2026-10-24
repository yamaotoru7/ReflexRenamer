using System.Globalization;
using System.Reflection;
using FlexibleRenamer.Core.Localization;
using FlexibleRenamer.Core.Resources;
using FlexibleRenamer.Core.Settings;

namespace FlexibleRenamer.Ui;

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

    /// <summary>実行ディレクトリの Flexible Renamer.ini。終了時に保存する。</summary>
    public AppSettings Settings { get; }

    public ResourceSet Resources { get; }

    public LanguageStrings Strings { get; }

    public string BaseDirectory { get; }

    public DialogBuilder Builder { get; }

    /// <summary>オリジナル版の既定フォント（Flexible Renamer.ini の FontFace=Tahoma、高さ 12px）。</summary>
    public static Font UiFont { get; set; } = new("Tahoma", 9f);

    public string T(string key) => Strings.Translate(key);

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
