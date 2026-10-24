using System.Text;
using System.Text.Json;
using ReflexRenamer.Core.Scripting;

namespace ReflexRenamer.Core.Tests;

/// <summary>
/// StrFunc.dll 互換の関数を、原本DLLの実測（docs/research/strfunc-sample.json、tools/FrTools/strfunc-reference/Probe.ps1 で採取）と照合する。
/// 実測は日本語の Windows で取っているので、曜日名などは同じ環境でしか一致しない。
/// </summary>
public class StrFuncTests
{
    private sealed record Sample(string Expr, string? Type, string? Value, int? Error, string? Description);

    /// <summary>原本の結果が不定（ポインターや前回の値を読む）なので照合しないケースと、その理由。</summary>
    private static readonly Dictionary<string, string> Unstable = new()
    {
        ["f.sprintf(\"[%d]\", \"12\")"] = "文字列のアドレスを数値として読む",
        ["f.sprintf(\"[%c]\", \"A\")"] = "文字列のアドレスを文字として読む",
        ["f.sprintf(\"[%x]\", \"ff\")"] = "文字列のアドレスを数値として読む",
        ["f.GetDateFormat(0, 0, \"2003/01/15\", \"yyyy-MM-dd\")"] = "日付以外の値は読まず、前回の日付が残る（移植版は日付に変換する）",
        ["f.GetDateFormat(0, 0, 37636, \"yyyy-MM-dd\")"] = "同上",
        ["f.GetDateFormat(0, 0, \"abc\", \"yyyy-MM-dd\")"] = "同上（移植版は型が一致しないエラー）",
        ["f.GetDateFormat(0, 0, Null, \"yyyy-MM-dd\")"] = "同上（移植版は型が一致しないエラー）",
        ["f.GetDateFormat(0, 0, Empty, \"yyyy-MM-dd\")"] = "同上（移植版は 1899-12-30）",
        ["f.GetTimeFormat(0, 0, \"15:04:05\", \"HH:mm:ss\")"] = "同上",
        ["f.GetTimeFormat(0, 0, \"abc\", \"HH:mm:ss\")"] = "同上",
        ["f.GetTimeFormat(0, 0, Null, \"HH:mm:ss\")"] = "同上",
        ["f.sprintf(\"[%d|%d]\", vn, 7)"] = "参照渡しの数値を積まない（移植版は値として積む）",
    };

    private static IReadOnlyList<Sample> Samples()
    {
        string path = Path.Combine(ReferenceFiles.RepoRoot, "docs", "research", "strfunc-sample.json");
        return JsonSerializer.Deserialize<List<Sample>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static ScriptHost CreateHost()
    {
        var host = new ScriptHost(ScriptLanguage.VBScript);
        host.AddCode("Set f = CreateObject(\"StrFunc.Wrapper\")\nvs = \"abc\" : vn = 5 : vd = #2003-01-15 10:20:30#");
        return host;
    }

    [Fact]
    public void MatchesOriginalDllSamples()
    {
        var samples = Samples();
        Assert.True(samples.Count > 300);
        using var host = CreateHost();
        var failures = new List<string>();
        int compared = 0;
        foreach (var sample in samples)
        {
            if (Unstable.ContainsKey(sample.Expr)) continue;
            compared++;
            string? type = null, value = null, error = null;
            try
            {
                type = host.Evaluate($"TypeName({sample.Expr})");
                value = host.Evaluate(sample.Expr);
            }
            catch (ScriptException e)
            {
                error = e.Message;
            }
            if (sample.Error is not null)
            {
                // ScriptControl の説明は「説明: '式'」、ScriptHost は「説明（1行 1文字目）」
                string expected = sample.Description!.Split(": '")[0];
                if (error is null || !error.StartsWith(expected, StringComparison.Ordinal))
                {
                    failures.Add($"{sample.Expr}\n  原本: エラー {sample.Error} {expected}\n  移植: {error ?? $"{value} ({type})"}");
                }
            }
            else if (error is not null || type != sample.Type || value != (sample.Value ?? ""))
            {
                failures.Add($"{sample.Expr}\n  原本: {Show(sample.Value)} ({sample.Type})\n  移植: {(error is null ? $"{Show(value)} ({type})" : "エラー " + error)}");
            }
        }
        Assert.True(failures.Count == 0, $"{failures.Count}/{compared} 件が不一致\n" + string.Join("\n", failures));
    }

    private static string Show(string? text) => text is null ? "null" : JsonSerializer.Serialize(text);

    [Fact]
    public void UnstableCasesExistInSamples()
    {
        var exprs = Samples().Select(s => s.Expr).ToHashSet();
        Assert.All(Unstable.Keys, key => Assert.Contains(key, exprs));
    }

    [Theory]
    [InlineData("toUpperCase(\"abc Def\")", "ABC DEF")]
    [InlineData("toLowerCase(\"ABC Def\")", "abc def")]
    [InlineData("toProperCase(\"aBC_def-ghi.jKL mno\")", "Abc_def-ghi.jkl Mno")]
    [InlineData("toWide(\"ABCabc 123\")", "ＡＢＣａｂｃ　１２３")]
    [InlineData("toNarrow(\"ＡＢＣ　アイウ\")", "ABC ｱｲｳ")]
    [InlineData("toKatakana(\"あいうゔ\")", "アイウヴ")]
    [InlineData("toHiragana(\"アイウヴ\")", "あいうゔ")]
    [InlineData("toSimplifiedChinese(\"國語學體\")", "国语学体")]
    [InlineData("toTraditionalChinese(\"国语学体\")", "國語學體")]
    [InlineData("toLinguisticCasing(\"abc\")", "")]
    [InlineData("StrConv(\"<あいうアイウｱｲｳABCabcＡＢＣａｂｃ> aBC_DEF　ａＢＣ＿ＤＥＦ\", vbProperCase Or vbKatakana Or vbNarrow)", "<ｱｲｳｱｲｳｱｲｳabcabcabcabc> Abc_def Abc_def")]
    [InlineData("StrFunc().sprintf(\"%s=%08x, %s=%10.8lf\", \"Value\", 512, \"PI\", 3.141592)", "Value=00000200, PI=3.14159200")]
    [InlineData("StrFunc().vsprintf(\"%03d-%s\", Array(7, \"x\"))", "007-x")]
    [InlineData("StrFunc().LCMapString(0, LCMAP_HIRAGANA Or LCMAP_PROPERCASE Or LCMAP_FULLWIDTH, \"<あいうアイウｱｲｳABCabcＡＢＣａｂｃ> aBC_DEF　ａＢＣ＿ＤＥＦ\")", "＜あいうあいうあいうａｂｃａｂｃａｂｃａｂｃ＞　Ａｂｃ＿ｄｅｆ　Ａｂｃ＿ｄｅｆ")]
    [InlineData("StrFunc().GetDateFormat(0, 0, #2003-01-15 10:20:30#, \"yyyy-MM-dd dddd\")", "2003-01-15 水曜日")]
    [InlineData("StrFunc().GetTimeFormat(0, 0, #2003-01-15 15:04:05#, \"hh:mm:ss tt\")", "03:04:05 午後")]
    [InlineData("StrFunc().vbProperCase + StrFunc().LCMAP_UPPERCASE", "515")]
    public void PresetScriptFunctionsUseStrFunc(string expression, string expected)
    {
        using var host = new ScriptHost(ScriptLanguage.VBScript);
        host.AddCode(File.ReadAllText(ReferenceFiles.Get("Script.Preset.vbs"), Encoding.Unicode));
        Assert.Equal(expected, host.Evaluate(expression));
    }

    [Fact]
    public void PresetStrConvPassesVariablesByReference()
    {
        // Script.Preset.vbs の StrConv(SrcStr, Conversion) は変数を参照渡しで原本に渡す
        using var host = new ScriptHost(ScriptLanguage.VBScript);
        host.AddCode(File.ReadAllText(ReferenceFiles.Get("Script.Preset.vbs"), Encoding.Unicode));
        host.AddCode("Function Twice(s)\nDim t : t = s & s\nTwice = StrConv(t, vbUpperCase)\nEnd Function");
        Assert.Equal("ABAB", host.Evaluate("Twice(\"ab\")"));
    }

    [Fact]
    public void OtherProgIdsStillCreateObjects()
    {
        using var vbs = new ScriptHost(ScriptLanguage.VBScript);
        Assert.Equal("Dictionary", vbs.Evaluate("TypeName(CreateObject(\"Scripting.Dictionary\"))"));
        Assert.Throws<ScriptException>(() => vbs.Evaluate("CreateObject(\"No.Such.ProgId\")"));

        using var js = new ScriptHost(ScriptLanguage.JScript);
        Assert.Equal("1", js.Evaluate("(function () { var d = new ActiveXObject(\"Scripting.Dictionary\"); d.Add(\"a\", 1); return d.Count; })()"));
    }

    [Fact]
    public void JScriptCanUseStrFunc()
    {
        using var host = new ScriptHost(ScriptLanguage.JScript);
        Assert.Equal("ABC", host.Evaluate("new ActiveXObject(\"StrFunc.Wrapper\").StrConv(\"abc\", 1)"));
        Assert.Equal("Abc Def", host.Evaluate("new ActiveXObject(\"strfunc.wrapper\").StrConv(\"abc def\", 3, 0)"));
        Assert.Equal("[007|x]", host.Evaluate("new ActiveXObject(\"StrFunc.Wrapper\").sprintf(\"[%03d|%s]\", 7, \"x\")"));
        Assert.Equal("512", host.Evaluate("new ActiveXObject(\"StrFunc.Wrapper\").LCMAP_UPPERCASE"));
        Assert.Empty(host.FunctionNames());
    }

    [Theory]
    [InlineData("[%d|%5.2f|%-4s|%x]", "[-3| 2.50|ab  |ff]")]
    public void SprintfFromCSharp(string format, string expected)
    {
        Assert.Equal(expected, StrFunc.Sprintf(format, -3, 2.5, "ab", 255));
    }

    [Fact]
    public void DirectCallsMatchSamples()
    {
        Assert.Equal("Abc_def-ghi.jkl Mno", StrFunc.StrConv("aBC_def-ghi.jKL mno", StrFunc.vbProperCase));
        Assert.Equal("", StrFunc.StrConv("abc", StrFunc.vbNone));
        Assert.Equal("ＡＢＣ", StrFunc.LCMapString(1041, StrFunc.LCMAP_FULLWIDTH, "ABC"));
        Assert.Equal("10:20:30", StrFunc.GetTimeFormat(0, 0, new DateTime(2003, 1, 15, 10, 20, 30), "HH:mm:ss"));
    }
}
