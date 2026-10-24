namespace FlexibleRenamer.Core.Scripting;

/// <summary>原本のスクリプトメニューから採取した埋め込み関数の表示名と引数表記。</summary>
public static class ScriptBuiltins
{
    public static IReadOnlyList<string> For(ScriptLanguage language)
    {
        using var stream = typeof(ScriptBuiltins).Assembly.GetManifestResourceStream($"FlexibleRenamer.Core.Scripting.{language}.Builtins.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }
}
