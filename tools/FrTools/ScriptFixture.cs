using System.Text;
using System.Text.Json;

namespace FrTools;

/// <summary>ユーザースクリプトをシナリオの間だけ差し替え、既存の内容を終了時に戻す。</summary>
internal sealed class ScriptFixture : IDisposable
{
    private readonly Dictionary<string, byte[]?> _original = [];

    public ScriptFixture(AppKind kind, JsonElement root)
    {
        if (!root.TryGetProperty("scripts", out var scripts)) return;
        try
        {
            foreach (var property in scripts.EnumerateObject())
            {
                string path = ScriptPath(kind, property.Name);
                _original[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
                File.WriteAllText(path, property.Value.GetString(), Encoding.Unicode);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public static string ScriptPath(AppKind kind, string name)
    {
        if (name is not ("Script.vbs" or "Script.js")) throw new ArgumentException("Script.vbs / Script.js のみ指定できる");
        return Path.Combine(Path.GetDirectoryName(kind == AppKind.Original ? Sandbox.OriginalExe : Sandbox.CloneExe)!, name);
    }

    public void Dispose()
    {
        foreach (var (path, bytes) in _original)
        {
            if (bytes is null) File.Delete(path);
            else File.WriteAllBytes(path, bytes);
        }
        _original.Clear();
    }
}
