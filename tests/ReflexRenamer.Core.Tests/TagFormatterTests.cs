using System.Globalization;
using ReflexRenamer.Core.Localization;
using ReflexRenamer.Core.Tags;

namespace ReflexRenamer.Core.Tests;

public class TagFormatterTests
{
    private static readonly LanguageStrings Japanese = LanguageStrings.Load(ReferenceFiles.Get("Flexible Renamer.JA.Strings.ini"));

    private static string? Picture(string key) => Japanese.Lookup("PictureTag", key);

    [Fact]
    public void ExpandsTagsAndLeavesOtherText()
    {
        var tags = new TagSet();
        tags.Set("ID3.Artist", "Band");
        tags.Set("ID3.Track", "Song");
        Assert.Equal("Band - Song ??.\\e", TagFormatter.Expand("<ID3.Artist> - <ID3.Track> ??.\\e", tags, new TagFormatOptions()));
    }

    [Fact]
    public void MissingTagBecomesEmpty()
    {
        Assert.Equal("[]", TagFormatter.Expand("[<ID3.Album>]", new TagSet(), new TagFormatOptions()));
    }

    [Fact]
    public void InvalidCharsAreReplacedPerSettings()
    {
        var tags = new TagSet();
        tags.Set("ID3.Track", "a/b:c*d?e\"f<g>h|i\\j");
        var options = new TagFormatOptions
        {
            InvalidCharReplacements = new Dictionary<char, string> { ['/'] = "-", [':'] = ".", ['*'] = "", ['?'] = "", ['"'] = "'", ['<'] = "(", ['>'] = ")", ['|'] = "_", ['\\'] = "" },
        };
        Assert.Equal("a-b.cde'f(g)h_ij", TagFormatter.Expand("<ID3.Track>", tags, options));
    }

    [Fact]
    public void FormatsDateTimeWithStrftime()
    {
        var tags = new TagSet();
        tags.Set("EXIF.OriginalTime", new DateTime(2010, 11, 12, 13, 14, 15));
        var options = new TagFormatOptions { DateTimeFormat = "%Y%m%d_%H%M%S", Culture = CultureInfo.InvariantCulture };
        Assert.Equal("20101112_131415", TagFormatter.Expand("<EXIF.OriginalTime>", tags, options));
    }

    [Fact]
    public void FormatsExifEnumsAndExposure()
    {
        var tags = new TagSet();
        tags.Set("EXIF.Flash", "0001");
        tags.Set("EXIF.MeteringMode", "00ff");
        tags.Set("EXIF.ExposureTime", "0.004");
        tags.Set("EXIF.ExposureCompensation", "-1");
        Assert.Equal("フラッシュ", TagFormatter.Expand("<EXIF.Flash>", tags, new TagFormatOptions(), Picture));
        Assert.Equal("予約", TagFormatter.Expand("<EXIF.MeteringMode>", tags, new TagFormatOptions(), Picture));
        // 「1/250 秒」の「/」はファイル名に使えないので、設定の置き換え（ここでは全角）を通る
        var slashToFullwidth = new TagFormatOptions { InvalidCharReplacements = new Dictionary<char, string> { ['/'] = "／" } };
        Assert.Equal("1／250 秒", TagFormatter.Expand("<EXIF.ExposureTime>", tags, slashToFullwidth, Picture).Replace("‎", ""));
        Assert.Equal("-1 ステップ", TagFormatter.Expand("<EXIF.ExposureCompensation>", tags, new TagFormatOptions(), Picture));
    }

    [Fact]
    public void LooksUpTagCaptions()
    {
        Assert.Equal("アーティスト", Japanese.Lookup("MusicTag", "<ID3.Artist>"));
        Assert.Equal("CDアルバムなどの楽曲情報です。", Japanese.Lookup("MusicTag", "Description"));
        Assert.Null(Japanese.Lookup("MusicTag", "<Nope>"));
    }

    [Fact]
    public void UnknownTagsOfTheKindStayLiteral()
    {
        // 種類の一覧にないタグは文字のまま、あって値のないタグは空（正解データ music-unknown-tag、music-v1）
        var known = new HashSet<string>(["ID3.Artist", "ID3v1.Artist"], StringComparer.OrdinalIgnoreCase);
        var tags = new TagSet();
        tags.Set("ID3.Artist", "Band");
        Assert.Equal("Band_<ID3.Nope>x<Nope>_", TagFormatter.Expand("<ID3.Artist>_<ID3.Nope>x<Nope>_<ID3v1.Artist>", tags, new TagFormatOptions(), null, known.Contains));
    }

    [Fact]
    public void DefaultsMatchOriginalSettingsDialog()
    {
        // 既定の置き換え先（\→$ /→% :→; *→# ?→@ "→'' <→[ >→] |→!）と日時の書式
        var tags = new TagSet();
        tags.Set("ID3.Track", "a\\b/c:d*e?f\"g<h>i|j");
        Assert.Equal("a$b%c;d#e@f''g[h]i!j", TagFormatter.Expand("<ID3.Track>", tags, new TagFormatOptions()));
        Assert.Equal(@"\Y\m\d\H\M\S", TagFormatOptions.DefaultDateTimeFormat);
    }

    [Fact]
    public void IsoSpeedGetsPrefix()
    {
        var tags = new TagSet();
        tags.Set("EXIF.ISOSpeed", "400");
        Assert.Equal("ISO-400", TagFormatter.Expand("<EXIF.ISOSpeed>", tags, new TagFormatOptions(), Picture));
    }
}
