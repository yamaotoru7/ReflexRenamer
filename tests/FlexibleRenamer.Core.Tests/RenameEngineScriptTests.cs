using FlexibleRenamer.Core.Presets;
using FlexibleRenamer.Core.Rename;
using FlexibleRenamer.Core.Scripting;

namespace FlexibleRenamer.Core.Tests;

public class RenameEngineScriptTests
{
    private static FileItem Item(string name) => new(Path.Combine(@"C:\work", name), false, 1, new DateTime(2012, 1, 1), new DateTime(2012, 1, 1), new DateTime(2012, 1, 1));

    [Fact]
    public void ScriptErrorLeavesNameUnchanged()
    {
        // スクリプトの評価に失敗した項目は名前が変わらない（オリジナル版：定型メニュー「桁をそろえる」で ＡＢＣ１２３.txt はそのまま）
        var options = new RenameOptions
        {
            Mode = RenameMode.RegularExpression,
            Search = @"(\d+)",
            Replace = @"X(\1)",
            Script = code => code.Contains('１') ? throw new ScriptException("文字が正しくありません。") : code.Replace("X(", "[").Replace(")", "]"),
        };
        var rows = new RenameEngine(options).Preview([Item("song12.mp3"), Item("ＡＢＣ１２３.txt")]);
        Assert.Equal("song[12].mp[3]", rows[0].NewName);
        Assert.Equal("ＡＢＣ１２３.txt", rows[1].NewName);
    }

    private static readonly Lazy<ScriptWorkspace> Scripts = new(() => new ScriptWorkspace(Path.GetDirectoryName(ReferenceFiles.Get("Script.Preset.vbs"))!));

    [Fact]
    public void TypicalScriptUsesPresetFunctions()
    {
        // 定型メニュー「全角カタカナに」（VBScript の toFullKana）
        var typical = PresetFile.Load(ReferenceFiles.Get("Preset.ini")).Typicals.First(t => t.Name == "to Fullwidth Katakana[JP only]");
        var expanded = TypicalTemplate.Expand(typical, new TypicalParameters(), null);
        Assert.Equal("VBScript", expanded.Script);
        var options = new RenameOptions
        {
            Mode = expanded.Mode,
            Search = expanded.Search,
            Replace = expanded.Replace,
            Script = Scripts.Value.Host(ScriptLanguage.VBScript).Evaluate,
        };
        var rows = new RenameEngine(options).Preview([Item("ﾃｽﾄｶﾅ.txt")]);
        Assert.Equal("テストカナ.txt", rows[0].NewName);
    }

    [Fact]
    public void ScriptCanCallStrFuncThroughPresetScript()
    {
        // Script.Preset.vbs の toProperCase は CreateObject("StrFunc.Wrapper") を使う（移植版は互換オブジェクトを返す）
        var options = new RenameOptions
        {
            Mode = RenameMode.RegularExpression,
            Search = ".*",
            Replace = "toProperCase(\"\\0\")",
            Script = Scripts.Value.Host(ScriptLanguage.VBScript).Evaluate,
        };
        var rows = new RenameEngine(options).Preview([Item("hello wORLD_x.txt")]);
        Assert.Equal("Hello World_x.txt", rows[0].NewName);
    }
}

public class NaturalOrderTests
{
    [Fact]
    public void SortsLikeOriginalHeaderClick()
    {
        // explore-sort の実測：数字は数値、英字は大文字小文字無視、日本語は文字コード順（カタカナ → 全角英字 → 半角カナ）
        string[] names = ["テスト ファイル.txt", "ﾃｽﾄｶﾅ.txt", "song10.mp3", "README", "report - Copy (2).txt", "ＡＢＣ１２３.txt", "song2.mp3", "サブフォルダ", "a-b.c-d.txt", "Copy of report.txt"];
        var sorted = names.OrderBy(n => n, NaturalOrder.Instance).ToArray();
        Assert.Equal(["a-b.c-d.txt", "Copy of report.txt", "README", "report - Copy (2).txt", "song2.mp3", "song10.mp3", "サブフォルダ", "テスト ファイル.txt", "ＡＢＣ１２３.txt", "ﾃｽﾄｶﾅ.txt"], sorted);
    }
}
