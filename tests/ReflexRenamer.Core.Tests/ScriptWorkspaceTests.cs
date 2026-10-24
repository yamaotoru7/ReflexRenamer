using System.Text;
using ReflexRenamer.Core.Scripting;

namespace ReflexRenamer.Core.Tests;

public sealed class ScriptWorkspaceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FrScriptTests-" + Guid.NewGuid());

    public ScriptWorkspaceTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void LoadsPresetAndUserFunctionsAndReloadsSavedEdits()
    {
        File.WriteAllText(Path.Combine(_directory, "Script.Preset.vbs"), "Function PresetValue()\nPresetValue = 2\nEnd Function", Encoding.Unicode);
        string user = Path.Combine(_directory, "Script.vbs");
        File.WriteAllText(user, "Function Custom()\nCustom = PresetValue() + 3\nEnd Function", Encoding.Unicode);
        using var scripts = new ScriptWorkspace(_directory);
        Assert.Equal("5", scripts.Host(ScriptLanguage.VBScript).Evaluate("Custom()"));
        Assert.Equal(new[] { "PresetValue", "Custom" }, scripts.Host(ScriptLanguage.VBScript).FunctionNames());
        File.WriteAllText(user, "Function Changed()\nChanged = 9\nEnd Function", Encoding.Unicode);
        Assert.Equal("9", scripts.Host(ScriptLanguage.VBScript).Evaluate("Changed()"));
        Assert.DoesNotContain("Custom", scripts.Host(ScriptLanguage.VBScript).FunctionNames());
    }

    [Fact]
    public void InvalidEditDoesNotKeepOldFunctionsAndCanBeCorrected()
    {
        using var scripts = new ScriptWorkspace(_directory);
        string path = scripts.EnsureUserFile(ScriptLanguage.JScript);
        File.WriteAllText(path, "function Custom() { return 1; }");
        Assert.Equal("1", scripts.Host(ScriptLanguage.JScript).Evaluate("Custom()"));
        File.WriteAllText(path, "function Custom( {");
        Assert.Throws<ScriptException>(() => scripts.Host(ScriptLanguage.JScript));
        File.WriteAllText(path, "function Custom() { return 2; }");
        Assert.Equal("2", scripts.Host(ScriptLanguage.JScript).Evaluate("Custom()"));
    }

    [Theory]
    [InlineData(ScriptLanguage.VBScript, "Script.vbs")]
    [InlineData(ScriptLanguage.JScript, "Script.js")]
    public void EditingTargetsUserFileWithoutOverwritingIt(ScriptLanguage language, string name)
    {
        using var scripts = new ScriptWorkspace(_directory);
        string path = scripts.EnsureUserFile(language);
        Assert.Equal(Path.Combine(_directory, name), path);
        Assert.Equal(new byte[] { 0xff, 0xfe }, File.ReadAllBytes(path));
        File.WriteAllText(path, "saved content", Encoding.Unicode);
        Assert.Equal(path, scripts.EnsureUserFile(language));
        Assert.Equal("saved content", File.ReadAllText(path));
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
