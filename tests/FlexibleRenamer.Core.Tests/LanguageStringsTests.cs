using FlexibleRenamer.Core.Localization;

namespace FlexibleRenamer.Core.Tests;

public class LanguageStringsTests
{
    private static readonly LanguageStrings Japanese = LanguageStrings.Load(ReferenceFiles.Get("Flexible Renamer.JA.Strings.ini"));

    [Fact]
    public void ReadsHeader()
    {
        Assert.Equal("ja-JP", Japanese.Code);
        Assert.Equal("JA", Japanese.ShortName);
        Assert.Equal("日本語", Japanese.Name);
    }

    [Fact]
    public void TranslatesWithNestedTokens()
    {
        // 「Specify File=%%File%%%%Specify%%」のように、訳の中の %%Key%% も展開する
        Assert.Equal("ファイル指定", Japanese.Translate("Specify File"));
        Assert.Equal("ファイル(&F)", Japanese.Translate("&File"));
    }

    [Fact]
    public void UnescapesKeysAndValues()
    {
        Assert.Equal("指定例: (.*)\\.(...)", Japanese.Translate("Ex.: (.*)\\.(...)"));
        Assert.Equal("終了(&X)\tAlt+F4", Japanese.Translate("E&xit\tAlt+F4"));
    }

    [Fact]
    public void ExpandsPresetTokens()
    {
        Assert.Equal(@"\s*[(|（|【\[].*?[\]|】|）|)]\s*", Japanese.Expand(@"\s*[%%(|\[%%].*?[%%\]||)%%]\s*"));
        // キーと値の前後の空白は削られる（GetPrivateProfileString と同じ）
        Assert.Equal("(.*)へのショートカット(.*)?", Japanese.Expand("(.*)%%Shortcut to %%(.*)?"));
    }

    [Fact]
    public void KeysAreCaseInsensitiveAndFirstWins()
    {
        // GetPrivateProfileString と同じく大文字小文字を区別しない。同じキーが複数あれば最初のもの
        Assert.Equal("カタカナ ⇔ ひらがな（拡張子を除く）", Japanese.TranslatePreset("Swap Hiragana and Katakana(w/o extension)[JP only]"));
        Assert.Equal("\\U, \\L, \\Q の効果終了\t\\E", Japanese.Translate("Lapse Effect Of \\U, \\L, \\Q\t\\E"));
        Assert.Equal("指定例: A-Za-z", Japanese.Translate("Ex.: a-zA-Z"));
    }

    [Fact]
    public void UnknownKeyFallsBackToKey()
    {
        Assert.Equal("NoSuchKey", Japanese.Translate("NoSuchKey"));
    }

    [Fact]
    public void TranslatesPresetNames()
    {
        Assert.Equal("語頭を大文字", Japanese.TranslatePreset("Capitalize"));
    }
}
