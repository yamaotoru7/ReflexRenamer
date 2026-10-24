using System.Text;

namespace FlexibleRenamer.Core.Scripting;

/// <summary>付属スクリプトとユーザースクリプトを読み込み、保存された変更を次の評価に反映する。</summary>
public sealed class ScriptWorkspace(string directory) : IDisposable
{
    private sealed record Loaded(string Preset, string User, ScriptHost Host);
    private readonly Dictionary<ScriptLanguage, Loaded> _loaded = [];

    private static string Extension(ScriptLanguage language) => language == ScriptLanguage.JScript ? "js" : "vbs";

    public string EnsureUserFile(ScriptLanguage language)
    {
        string path = Path.Combine(directory, "Script." + Extension(language));
        if (!File.Exists(path))
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            stream.Write(Encoding.Unicode.GetPreamble());
        }
        return path;
    }

    public ScriptHost Host(ScriptLanguage language)
    {
        string extension = Extension(language);
        string Read(string name)
        {
            string path = Path.Combine(directory, name + extension);
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        string preset = Read("Script.Preset.");
        string user = Read("Script.");
        if (_loaded.TryGetValue(language, out var loaded))
        {
            if (loaded.Preset == preset && loaded.User == user) return loaded.Host;
            _loaded.Remove(language);
            loaded.Host.Dispose();
        }
        var host = new ScriptHost(language);
        try
        {
            if (preset.Length > 0) host.AddCode(preset);
            if (user.Length > 0) host.AddCode(user);
            _loaded.Add(language, new Loaded(preset, user, host));
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var loaded in _loaded.Values) loaded.Host.Dispose();
        _loaded.Clear();
    }
}
