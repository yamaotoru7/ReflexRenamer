using System.Text;
using ReflexRenamer.Core.Scripting;

namespace ReflexRenamer.Core.Tests;

public class ScriptHostTests
{
    [Fact]
    public void EvaluatesVBScriptExpression()
    {
        using var host = new ScriptHost(ScriptLanguage.VBScript);
        Assert.Equal("123123", host.Evaluate("\"123\" & \"123\""));
        Assert.Equal("246", host.Evaluate("123+123"));
    }

    [Fact]
    public void EvaluatesJScriptExpression()
    {
        using var host = new ScriptHost(ScriptLanguage.JScript);
        Assert.Equal("123123", host.Evaluate("\"123\"+\"123\""));
    }

    [Fact]
    public void UsesFunctionsFromPresetScript()
    {
        using var host = new ScriptHost(ScriptLanguage.VBScript);
        host.AddCode(File.ReadAllText(ReferenceFiles.Get("Script.Preset.vbs"), Encoding.Unicode));
        Assert.Equal("007", host.Evaluate("DigitNumber(7, 3, vbNone)"));
        Assert.Equal("ハンカクカナ", host.Evaluate("toFullKana(\"ﾊﾝｶｸｶﾅ\")"));
    }

    [Fact]
    public void ReportsSyntaxError()
    {
        using var host = new ScriptHost(ScriptLanguage.VBScript);
        Assert.Throws<ScriptException>(() => host.Evaluate("1 +"));
    }

    [Fact]
    public void ListsPresetFunctionsInDeclarationOrder()
    {
        using var host = new ScriptHost(ScriptLanguage.VBScript);
        host.AddCode(File.ReadAllText(ReferenceFiles.Get("Script.Preset.vbs"), Encoding.Unicode));
        var names = host.FunctionNames();
        Assert.Equal(19, names.Count);
        Assert.Equal(new[] { "DigitNumber", "toFullKana", "toHalfKana" }, names.Take(3));
        Assert.Equal("toLinguisticCasing", names[^1]);
    }

    [Theory]
    [InlineData(ScriptLanguage.VBScript, "Function Custom(value)\nCustom = value\nEnd Function")]
    [InlineData(ScriptLanguage.JScript, "function Custom(value) { return value; }")]
    public void ListsUserFunctions(ScriptLanguage language, string code)
    {
        using var host = new ScriptHost(language);
        host.AddCode(code);
        Assert.Contains("Custom", host.FunctionNames());
    }
}
