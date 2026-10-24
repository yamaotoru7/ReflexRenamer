using System.Buffers.Binary;
using System.Text;
using FlexibleRenamer.Core.Tags;

namespace FlexibleRenamer.Core.Tests;

/// <summary>
/// ASF（WMA）、APEv2（APE）、IPTC（JPEG APP13 / PSD）の各フォーマット仕様に従った
/// 合成バイト列によるタグ読み取りテスト。
/// </summary>
public sealed class TagReaderFormatTests : IDisposable
{
    private static readonly Guid AsfHeaderObject = new("75B22630-668E-11CF-A6D9-00AA0062CE6C");
    private static readonly Guid AsfContentDescription = new("75B22633-668E-11CF-A6D9-00AA0062CE6C");
    private static readonly Guid AsfExtendedContentDescription = new("D2D0A440-E307-11D2-97F0-00A0C95EA850");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FlexibleRenamerFormatTests", Guid.NewGuid().ToString("N"));

    public TagReaderFormatTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string Write(string name, byte[] bytes)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    #region ASF (WMA) Tests

    [Fact]
    public void ReadsAsfContentDescription()
    {
        // Content Description: Title, Author, Copyright, Description, Rating
        byte[] cd = BuildContentDescription("Song Title", "Artist Name", "(C) 2024 Author", "Test Song Description", "5");
        byte[] fileBytes = BuildAsf(contentDescription: cd);
        string path = Write("test.wma", fileBytes);

        var directTags = AsfReader.Read(path);
        Assert.Equal("Song Title", directTags["WMA.Title"].Text);
        Assert.Equal("Artist Name", directTags["WMA.Author"].Text);
        Assert.Equal("(C) 2024 Author", directTags["WMA.Copyright"].Text);
        Assert.Equal("Test Song Description", directTags["WMA.Description"].Text);
        Assert.Equal("5", directTags["WMA.Rating"].Text);

        // ID3.* へのマッピング
        Assert.Equal("Song Title", directTags["ID3.Track"].Text);
        Assert.Equal("Artist Name", directTags["ID3.Artist"].Text);
        Assert.Equal("Test Song Description", directTags["ID3.Comment"].Text);

        // TagReader 経由での読み取り
        var tags = TagReader.Read(path);
        Assert.Equal("Song Title", tags["ID3.Track"].Text);
        Assert.Equal("Artist Name", tags["ID3.Artist"].Text);
        Assert.Equal("Test Song Description", tags["ID3.Comment"].Text);
    }

    [Fact]
    public void ReadsAsfExtendedContentDescriptionWithStringValues()
    {
        // Extended Content Description: 文字列型（type 0）の WM/AlbumTitle, WM/TrackNumber, WM/Year, WM/Genre, WM/AlbumArtist
        byte[] ext = BuildExtendedContent(
            ExtString("WM/AlbumTitle", "Test Album"),
            ExtString("WM/TrackNumber", "3/12"),
            ExtString("WM/Year", "2024"),
            ExtString("WM/Genre", "Rock"),
            ExtString("WM/AlbumArtist", "Band Name")
        );
        byte[] fileBytes = BuildAsf(extendedContent: ext);
        string path = Write("ext_str.wma", fileBytes);

        var tags = AsfReader.Read(path);
        Assert.Equal("Test Album", tags["WMA.WM/AlbumTitle"].Text);
        Assert.Equal("3/12", tags["WMA.WM/TrackNumber"].Text);
        Assert.Equal("2024", tags["WMA.WM/Year"].Text);
        Assert.Equal("Rock", tags["WMA.WM/Genre"].Text);
        Assert.Equal("Band Name", tags["WMA.WM/AlbumArtist"].Text);

        // ID3.* へのマッピング（TrackNumber は Id3Reader.NormalizeTrackNumber により 2 桁化）
        Assert.Equal("Test Album", tags["ID3.Album"].Text);
        // 文字列型の WM/TrackNumber はオリジナル版では使われず -1 になる（golden tagrename-formats の wma2）
        Assert.Equal("-1", tags["ID3.TrackNumber"].Text);
        Assert.Equal("2024", tags["ID3.Year"].Text);
        Assert.Equal("Rock", tags["ID3.Genre"].Text);
        Assert.Equal("Band Name", tags["ID3.Artist"].Text);
    }

    [Fact]
    public void ReadsAsfExtendedContentDescriptionWithNumericDwordValues()
    {
        // Extended Content Description: DWORD 型（type 3）の WM/TrackNumber, WM/Year
        byte[] ext = BuildExtendedContent(
            ExtDword("WM/TrackNumber", 7),
            ExtDword("WM/Year", 2021)
        );
        byte[] fileBytes = BuildAsf(extendedContent: ext);
        string path = Write("ext_dword.wma", fileBytes);

        var tags = AsfReader.Read(path);
        Assert.Equal("7", tags["WMA.WM/TrackNumber"].Text);
        Assert.Equal("2021", tags["WMA.WM/Year"].Text);

        // ID3.* へのマッピング（7 -> "07"）
        Assert.Equal("07", tags["ID3.TrackNumber"].Text);
        Assert.Equal("2021", tags["ID3.Year"].Text);
    }

    [Fact]
    public void ReadsAsfCombinedContentAndExtendedWithPriorityAndFallback()
    {
        // Content Description と Extended Content Description の両方がある場合:
        // 1. Content Description の Author があれば、WM/AlbumArtist より優先される（SetIfMissing のため）
        // 2. WM/TrackNumber がなく WM/Track（DWORD）がある場合、ID3.TrackNumber に採用される
        byte[] cd = BuildContentDescription("Combined Title", "Primary Artist", null, null, null);
        byte[] ext = BuildExtendedContent(
            ExtString("WM/AlbumTitle", "Combined Album"),
            ExtString("WM/AlbumArtist", "Album Artist"),
            ExtDword("WM/Track", 9)
        );
        byte[] fileBytes = BuildAsf(contentDescription: cd, extendedContent: ext);
        string path = Write("combined.wma", fileBytes);

        var tags = AsfReader.Read(path);
        Assert.Equal("Primary Artist", tags["ID3.Artist"].Text);
        Assert.Equal("Combined Album", tags["ID3.Album"].Text);
        // WM/Track は 0 から数えるので 1 を足す（golden tagrename-formats の wma3）
        Assert.Equal("10", tags["ID3.TrackNumber"].Text);
    }

    [Fact]
    public void ReadsAsfCorruptedOrNonAsfFileReturnsEmpty()
    {
        // 先頭ヘッダが 30 バイト未満、または GUID が一致しないファイル
        string broken1 = Write("broken1.wma", [0x01, 0x02, 0x03]);
        Assert.Empty(AsfReader.Read(broken1).Values);

        byte[] fakeHeader = new byte[30];
        string broken2 = Write("broken2.wma", fakeHeader);
        Assert.Empty(AsfReader.Read(broken2).Values);
    }

    #endregion

    #region APEv2 Tests

    [Fact]
    public void ReadsApeV2WithoutHeader()
    {
        // ヘッダなし APEv2 タグ（ファイル末尾にフッターのみ）
        var items = new List<(string Key, string Value)>
        {
            ("Title", "APE Track Title"),
            ("Artist", "APE Artist"),
            ("Album", "APE Album"),
            ("Track", "4"),
            ("Year", "2022"),
            ("Genre", "Jazz"),
            ("Comment", "APE Comment Text"),
        };
        byte[] fileBytes = BuildApeFile(items, includeHeader: false);
        string path = Write("no_header.ape", fileBytes);

        var tags = ApeReader.Read(path);
        Assert.Equal("APE Track Title", tags["APE.Title"].Text);
        Assert.Equal("APE Artist", tags["APE.Artist"].Text);
        Assert.Equal("APE Album", tags["APE.Album"].Text);
        Assert.Equal("4", tags["APE.Track"].Text);
        Assert.Equal("2022", tags["APE.Year"].Text);
        Assert.Equal("Jazz", tags["APE.Genre"].Text);
        Assert.Equal("APE Comment Text", tags["APE.Comment"].Text);

        // ID3.* へのマッピング
        Assert.Equal("APE Track Title", tags["ID3.Track"].Text);
        Assert.Equal("APE Artist", tags["ID3.Artist"].Text);
        Assert.Equal("APE Album", tags["ID3.Album"].Text);
        Assert.Equal("04", tags["ID3.TrackNumber"].Text);
        Assert.Equal("2022", tags["ID3.Year"].Text);
        Assert.Equal("Jazz", tags["ID3.Genre"].Text);
        Assert.Equal("APE Comment Text", tags["ID3.Comment"].Text);

        // TagReader 経由
        var trTags = TagReader.Read(path);
        Assert.Equal("APE Track Title", trTags["ID3.Track"].Text);
        Assert.Equal("04", trTags["ID3.TrackNumber"].Text);
    }

    [Fact]
    public void ReadsApeV2WithHeader()
    {
        // ヘッダ付き APEv2 タグ（Header 32B + Items + Footer 32B）
        var items = new List<(string Key, string Value)>
        {
            ("Title", "Headered Song"),
            ("Artist", "Headered Artist"),
            ("Album", "Headered Album"),
            ("Track", "12/15"),
            ("Year", "2023"),
            ("Genre", "Acoustic"),
        };
        byte[] fileBytes = BuildApeFile(items, includeHeader: true);
        string path = Write("with_header.ape", fileBytes);

        var tags = ApeReader.Read(path);
        Assert.Equal("Headered Song", tags["APE.Title"].Text);
        Assert.Equal("Headered Artist", tags["APE.Artist"].Text);
        Assert.Equal("Headered Album", tags["APE.Album"].Text);
        Assert.Equal("12/15", tags["APE.Track"].Text);
        Assert.Equal("2023", tags["APE.Year"].Text);
        Assert.Equal("Acoustic", tags["APE.Genre"].Text);

        Assert.Equal("Headered Song", tags["ID3.Track"].Text);
        Assert.Equal("Headered Artist", tags["ID3.Artist"].Text);
        Assert.Equal("12", tags["ID3.TrackNumber"].Text);
    }

    [Fact]
    public void ReadsApeV2PrecedingId3v1()
    {
        // 末尾に ID3v1（128B）が存在する場合、その前にある APEv2 フッターを見つけて読む
        var items = new List<(string Key, string Value)>
        {
            ("Title", "APE Title"),
            ("Artist", "APE Artist"),
            ("Track", "5"),
        };
        byte[] id3v1 = BuildId3v1("Old Title", "Old Artist", "Old Album", "1995", 1, 17);
        byte[] fileBytes = BuildApeFile(items, includeHeader: false, id3v1Suffix: id3v1);
        string path = Write("with_v1.ape", fileBytes);

        var tags = ApeReader.Read(path);
        Assert.Equal("APE Title", tags["APE.Title"].Text);
        Assert.Equal("APE Artist", tags["APE.Artist"].Text);
        Assert.Equal("05", tags["ID3.TrackNumber"].Text);

        // TagReader 経由（.ape 拡張子）
        var trTags = TagReader.Read(path);
        Assert.Equal("APE Title", trTags["ID3.Track"].Text);
        Assert.Equal("05", trTags["ID3.TrackNumber"].Text);
    }

    [Fact]
    public void ReadsApeV2MultipleValuesNullSeparated()
    {
        // APEv2 仕様では複数値は NULL（0x00）で区切られる。
        // ApeReader は NULL を ';' に置換して格納する。
        var items = new List<(string Key, string Value)>
        {
            ("Artist", "Artist One\0Artist Two"),
        };
        byte[] fileBytes = BuildApeFile(items, includeHeader: false);
        string path = Write("multi_val.ape", fileBytes);

        var tags = ApeReader.Read(path);
        Assert.Equal("Artist One;Artist Two", tags["APE.Artist"].Text);
        Assert.Equal("Artist One;Artist Two", tags["ID3.Artist"].Text);
    }

    [Fact]
    public void ReadsApeBrokenFileReturnsEmpty()
    {
        string broken = Write("broken.ape", [0x41, 0x50, 0x45]); // 32 バイト未満
        Assert.Empty(ApeReader.Read(broken).Values);
    }

    #endregion

    #region IPTC Tests

    [Fact]
    public void ReadsIptcFromJpegApp13()
    {
        // JPEG APP13（Photoshop 3.0 / 8BIM 0x0404）内の IPTC-IIM レコード 2 の各フィールド
        var iimList = new List<byte>();
        iimList.AddRange(BuildIptcDataSet(2, 5, "Fuji Mountain"));                       // ObjectName
        iimList.AddRange(BuildIptcDataSet(2, 15, "NAT"));                                 // Category
        iimList.AddRange(BuildIptcDataSet(2, 80, "Yamada Taro"));                         // Byline
        iimList.AddRange(BuildIptcDataSet(2, 85, "Staff Photographer"));                  // BylineTitle
        iimList.AddRange(BuildIptcDataSet(2, 90, "Fujiyoshida"));                         // City
        iimList.AddRange(BuildIptcDataSet(2, 95, "Yamanashi"));                           // State
        iimList.AddRange(BuildIptcDataSet(2, 101, "Japan"));                              // Country
        iimList.AddRange(BuildIptcDataSet(2, 103, "TRANS-001"));                          // OriginalTransmissionReference
        iimList.AddRange(BuildIptcDataSet(2, 105, "Mount Fuji at Sunset"));               // Headline
        iimList.AddRange(BuildIptcDataSet(2, 110, "Japan Photo Agency"));                 // Credits
        iimList.AddRange(BuildIptcDataSet(2, 116, "(C) 2024 Yamada"));                    // CopyrightNotice
        iimList.AddRange(BuildIptcDataSet(2, 120, "A view of Mount Fuji at dusk"));       // Caption
        iimList.AddRange(BuildIptcDataSet(2, 122, "Editor Sato"));                        // CaptionWriter

        byte[] jpeg = BuildJpegWithIptc(iimList.ToArray());
        string path = Write("photo.jpg", jpeg);

        var directTags = IptcReader.Read(path);
        Assert.Equal("Fuji Mountain", directTags["IPTC.ObjectName"].Text);
        Assert.Equal("NAT", directTags["IPTC.Category"].Text);
        Assert.Equal("Yamada Taro", directTags["IPTC.Byline"].Text);
        Assert.Equal("Staff Photographer", directTags["IPTC.BylineTitle"].Text);
        Assert.Equal("Fujiyoshida", directTags["IPTC.City"].Text);
        Assert.Equal("Yamanashi", directTags["IPTC.State"].Text);
        Assert.Equal("Japan", directTags["IPTC.Country"].Text);
        Assert.Equal("TRANS-001", directTags["IPTC.OriginalTransmissionReference"].Text);
        Assert.Equal("Mount Fuji at Sunset", directTags["IPTC.Headline"].Text);
        Assert.Equal("Japan Photo Agency", directTags["IPTC.Credits"].Text);
        Assert.Equal("(C) 2024 Yamada", directTags["IPTC.CopyrightNotice"].Text);
        Assert.Equal("A view of Mount Fuji at dusk", directTags["IPTC.Caption"].Text);
        Assert.Equal("Editor Sato", directTags["IPTC.CaptionWriter"].Text);

        // TagReader 経由（JPEG では EXIF と IPTC を合成して返す）
        var tags = TagReader.Read(path);
        Assert.Equal("Fuji Mountain", tags["IPTC.ObjectName"].Text);
        Assert.Equal("Yamada Taro", tags["IPTC.Byline"].Text);
        Assert.Equal("Fujiyoshida", tags["IPTC.City"].Text);
        Assert.Equal("A view of Mount Fuji at dusk", tags["IPTC.Caption"].Text);
    }

    [Fact]
    public void ReadsIptcDateCreatedAsText()
    {
        // DateCreated（2:55）はオリジナル版では日時にせず、yyyyMMdd の文字列のまま（golden tagrename-formats の f-image-date）
        byte[] iim = BuildIptcDataSet(2, 55, "20240315");
        byte[] jpeg = BuildJpegWithIptc(iim);
        string path = Write("date.jpg", jpeg);

        var tags = IptcReader.Read(path);
        Assert.Equal("20240315", tags["IPTC.DateCreated"].Text);
    }

    [Fact]
    public void ReadsIptcUtf8WithCodedCharacterSet()
    {
        // 1:90（Coded Character Set）に ESC % G（0x1B, 0x25, 0x47）を指定して UTF-8 テキストを読み取る
        var iimList = new List<byte>();
        iimList.AddRange(BuildIptcDataSet(1, 90, [0x1B, 0x25, 0x47]));
        iimList.AddRange(BuildIptcDataSet(2, 5, "富士山", Encoding.UTF8));
        iimList.AddRange(BuildIptcDataSet(2, 90, "富士吉田市", Encoding.UTF8));
        iimList.AddRange(BuildIptcDataSet(2, 120, "夕暮れの富士山", Encoding.UTF8));

        byte[] jpeg = BuildJpegWithIptc(iimList.ToArray());
        string path = Write("utf8.jpg", jpeg);

        var tags = IptcReader.Read(path);
        Assert.Equal("富士山", tags["IPTC.ObjectName"].Text);
        Assert.Equal("富士吉田市", tags["IPTC.City"].Text);
        Assert.Equal("夕暮れの富士山", tags["IPTC.Caption"].Text);
    }

    [Fact]
    public void ReadsIptcFromPsd()
    {
        // PSD（8BPS）ファイル内の画像リソースセクション（8BIM 0x0404）から IPTC を読む
        byte[] iim = BuildIptcDataSet(2, 5, "PSD Graphic");
        byte[] psd = BuildPsdWithIptc(iim);
        string path = Write("image.psd", psd);

        var directTags = IptcReader.ReadPsd(path);
        Assert.Equal("PSD Graphic", directTags["IPTC.ObjectName"].Text);

        var tags = TagReader.Read(path);
        Assert.Equal("PSD Graphic", tags["IPTC.ObjectName"].Text);
    }

    [Fact]
    public void ReadsIptcInvalidJpegReturnsEmpty()
    {
        // APP13 セグメントのない単なる JPEG
        byte[] simpleJpeg = [0xFF, 0xD8, 0xFF, 0xD9];
        string path = Write("simple.jpg", simpleJpeg);

        Assert.Empty(IptcReader.Read(path).Values);
    }

    #endregion

    #region ASF Binary Builders

    private static byte[] BuildAsf(byte[]? contentDescription = null, byte[]? extendedContent = null)
    {
        var children = new List<byte>();
        int count = 0;
        if (contentDescription is not null)
        {
            children.AddRange(contentDescription);
            count++;
        }
        if (extendedContent is not null)
        {
            children.AddRange(extendedContent);
            count++;
        }

        long headerSize = 30 + children.Count;
        var header = new byte[30];
        AsfHeaderObject.ToByteArray().CopyTo(header, 0);
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16), headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), count);
        header[28] = 0x01; // Reserved 1
        header[29] = 0x02; // Reserved 2

        return [.. header, .. children];
    }

    private static byte[] BuildContentDescription(string? title, string? author, string? copyright, string? description, string? rating)
    {
        byte[] t = title is null ? [] : Encoding.Unicode.GetBytes(title + "\0");
        byte[] a = author is null ? [] : Encoding.Unicode.GetBytes(author + "\0");
        byte[] c = copyright is null ? [] : Encoding.Unicode.GetBytes(copyright + "\0");
        byte[] d = description is null ? [] : Encoding.Unicode.GetBytes(description + "\0");
        byte[] r = rating is null ? [] : Encoding.Unicode.GetBytes(rating + "\0");

        long objectSize = 24 + 10 + t.Length + a.Length + c.Length + d.Length + r.Length;
        var obj = new List<byte>();
        obj.AddRange(AsfContentDescription.ToByteArray());
        var sizeBytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(sizeBytes, objectSize);
        obj.AddRange(sizeBytes);

        var lengths = new byte[10];
        BinaryPrimitives.WriteUInt16LittleEndian(lengths.AsSpan(0), (ushort)t.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(lengths.AsSpan(2), (ushort)a.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(lengths.AsSpan(4), (ushort)c.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(lengths.AsSpan(6), (ushort)d.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(lengths.AsSpan(8), (ushort)r.Length);
        obj.AddRange(lengths);

        obj.AddRange(t);
        obj.AddRange(a);
        obj.AddRange(c);
        obj.AddRange(d);
        obj.AddRange(r);
        return obj.ToArray();
    }

    private static byte[] BuildExtendedContent(params (string Name, ushort Type, byte[] Value)[] descriptors)
    {
        var descList = new List<byte>();
        foreach (var (name, type, val) in descriptors)
        {
            byte[] nameBytes = Encoding.Unicode.GetBytes(name + "\0");
            var entryHeader = new byte[2 + nameBytes.Length + 4];
            BinaryPrimitives.WriteUInt16LittleEndian(entryHeader.AsSpan(0), (ushort)nameBytes.Length);
            nameBytes.CopyTo(entryHeader, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(entryHeader.AsSpan(2 + nameBytes.Length), type);
            BinaryPrimitives.WriteUInt16LittleEndian(entryHeader.AsSpan(2 + nameBytes.Length + 2), (ushort)val.Length);

            descList.AddRange(entryHeader);
            descList.AddRange(val);
        }

        long objectSize = 24 + 2 + descList.Count;
        var obj = new List<byte>();
        obj.AddRange(AsfExtendedContentDescription.ToByteArray());
        var sizeBytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(sizeBytes, objectSize);
        obj.AddRange(sizeBytes);

        var countBytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(countBytes, (ushort)descriptors.Length);
        obj.AddRange(countBytes);

        obj.AddRange(descList);
        return obj.ToArray();
    }

    private static (string Name, ushort Type, byte[] Value) ExtString(string name, string value) =>
        (name, 0, Encoding.Unicode.GetBytes(value + "\0"));

    private static (string Name, ushort Type, byte[] Value) ExtDword(string name, uint value)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        return (name, 3, b);
    }

    #endregion

    #region APEv2 Binary Builders

    private static byte[] BuildApeFile(IReadOnlyList<(string Key, string Value)> items, bool includeHeader, byte[]? audioPrefix = null, byte[]? id3v1Suffix = null)
    {
        audioPrefix ??= new byte[64]; // ダミーの音声データ

        var body = new List<byte>();
        foreach (var (k, v) in items)
        {
            body.AddRange(BuildApeItem(k, v));
        }

        // APEv2 仕様: Tag size はヘッダを含まず、アイテム群 + フッター（32B）のサイズ
        int tagSize = body.Count + 32;
        int count = items.Count;

        byte[] footer = new byte[32];
        Encoding.ASCII.GetBytes("APETAGEX").CopyTo(footer, 0);
        BinaryPrimitives.WriteInt32LittleEndian(footer.AsSpan(8), 2000); // Version 2.000
        BinaryPrimitives.WriteInt32LittleEndian(footer.AsSpan(12), tagSize);
        BinaryPrimitives.WriteInt32LittleEndian(footer.AsSpan(16), count);
        // Flags: bit 31 = has header, bit 29 = 0 (is footer)
        int footerFlags = includeHeader ? unchecked((int)0x80000000) : 0;
        BinaryPrimitives.WriteInt32LittleEndian(footer.AsSpan(20), footerFlags);

        byte[] header = [];
        if (includeHeader)
        {
            header = new byte[32];
            Encoding.ASCII.GetBytes("APETAGEX").CopyTo(header, 0);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), 2000);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), tagSize);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), count);
            // Flags: bit 31 = has header, bit 29 = is header (0x20000000)
            int headerFlags = unchecked((int)0xA0000000);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20), headerFlags);
        }

        var result = new List<byte>();
        result.AddRange(audioPrefix);
        if (includeHeader)
        {
            result.AddRange(header);
        }
        result.AddRange(body);
        result.AddRange(footer);
        if (id3v1Suffix is not null)
        {
            result.AddRange(id3v1Suffix);
        }
        return result.ToArray();
    }

    private static byte[] BuildApeItem(string key, string value)
    {
        byte[] valBytes = Encoding.UTF8.GetBytes(value);
        byte[] keyBytes = Encoding.ASCII.GetBytes(key);

        var item = new List<byte>();
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenBytes, valBytes.Length);
        item.AddRange(lenBytes);
        item.AddRange([0, 0, 0, 0]); // Flags (0 = UTF-8 text)
        item.AddRange(keyBytes);
        item.Add(0x00); // Key 終端
        item.AddRange(valBytes);
        return item.ToArray();
    }

    private static byte[] BuildId3v1(string title, string artist, string album, string year, byte track, byte genre)
    {
        var v1 = new byte[128];
        Encoding.ASCII.GetBytes("TAG").CopyTo(v1, 0);
        Encoding.ASCII.GetBytes(title.PadRight(30, '\0')[..30]).CopyTo(v1, 3);
        Encoding.ASCII.GetBytes(artist.PadRight(30, '\0')[..30]).CopyTo(v1, 33);
        Encoding.ASCII.GetBytes(album.PadRight(30, '\0')[..30]).CopyTo(v1, 63);
        Encoding.ASCII.GetBytes(year.PadRight(4, '\0')[..4]).CopyTo(v1, 93);
        v1[126] = track;
        v1[127] = genre;
        return v1;
    }

    #endregion

    #region IPTC Binary Builders

    private static byte[] BuildIptcDataSet(byte record, byte dataSet, byte[] data)
    {
        var tag = new byte[5 + data.Length];
        tag[0] = 0x1C;
        tag[1] = record;
        tag[2] = dataSet;
        BinaryPrimitives.WriteUInt16BigEndian(tag.AsSpan(3), (ushort)data.Length);
        data.CopyTo(tag, 5);
        return tag;
    }

    private static byte[] BuildIptcDataSet(byte record, byte dataSet, string text, Encoding? encoding = null) =>
        BuildIptcDataSet(record, dataSet, (encoding ?? Encoding.UTF8).GetBytes(text));

    private static byte[] BuildJpegWithIptc(byte[] iptcData)
    {
        // 8BIM リソースブロック (ID 0x0404: IPTC-NAA)
        // Pascal string name: 0x00, 0x00 (length 0 + 偶数パディングで計 2 バイト)
        var bim = new List<byte>();
        bim.AddRange("8BIM"u8.ToArray());
        var idBytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(idBytes, 0x0404);
        bim.AddRange(idBytes);
        bim.AddRange([0x00, 0x00]); // Pascal string
        var sizeBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sizeBytes, (uint)iptcData.Length);
        bim.AddRange(sizeBytes);
        bim.AddRange(iptcData);
        if (iptcData.Length % 2 == 1)
        {
            bim.Add(0x00); // 偶数境界パディング
        }

        // APP13 セグメント: 0xFF, 0xED, length (2B), "Photoshop 3.0\0", bim
        var app13Payload = new List<byte>();
        app13Payload.AddRange("Photoshop 3.0\0"u8.ToArray());
        app13Payload.AddRange(bim);

        int segmentLength = 2 + app13Payload.Count;
        var app13Header = new byte[4];
        app13Header[0] = 0xFF;
        app13Header[1] = 0xED;
        BinaryPrimitives.WriteUInt16BigEndian(app13Header.AsSpan(2), (ushort)segmentLength);

        return [0xFF, 0xD8, .. app13Header, .. app13Payload, 0xFF, 0xD9];
    }

    private static byte[] BuildPsdWithIptc(byte[] iptcData)
    {
        // 8BIM リソースブロック (ID 0x0404)
        var bim = new List<byte>();
        bim.AddRange("8BIM"u8.ToArray());
        var idBytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(idBytes, 0x0404);
        bim.AddRange(idBytes);
        bim.AddRange([0x00, 0x00]);
        var sizeBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sizeBytes, (uint)iptcData.Length);
        bim.AddRange(sizeBytes);
        bim.AddRange(iptcData);
        if (iptcData.Length % 2 == 1)
        {
            bim.Add(0x00);
        }

        // PSD ヘッダ (26 バイト)
        var psdHeader = new byte[26];
        Encoding.ASCII.GetBytes("8BPS").CopyTo(psdHeader, 0);
        BinaryPrimitives.WriteUInt16BigEndian(psdHeader.AsSpan(4), 1); // Version 1
        BinaryPrimitives.WriteUInt16BigEndian(psdHeader.AsSpan(12), 3); // 3 channels
        BinaryPrimitives.WriteUInt32BigEndian(psdHeader.AsSpan(14), 1); // Height 1
        BinaryPrimitives.WriteUInt32BigEndian(psdHeader.AsSpan(18), 1); // Width 1
        BinaryPrimitives.WriteUInt16BigEndian(psdHeader.AsSpan(22), 8); // Depth 8
        BinaryPrimitives.WriteUInt16BigEndian(psdHeader.AsSpan(24), 3); // RGB

        // ColorModeData Length (4 バイト: 0)
        var colorModeLength = new byte[4];

        // ImageResources Length (4 バイト) + bim
        var resLength = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(resLength, (uint)bim.Count);

        return [.. psdHeader, .. colorModeLength, .. resLength, .. bim];
    }

    #endregion
}
