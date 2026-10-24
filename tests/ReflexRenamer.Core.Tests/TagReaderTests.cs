using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ReflexRenamer.Core.Tags;

namespace ReflexRenamer.Core.Tests;

public sealed class TagReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ReflexRenamerTests", Guid.NewGuid().ToString("N"));

    public TagReaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, byte[] bytes)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void LoadsTagDefinitions()
    {
        var kinds = TagDefinitions.Load(ReferenceFiles.Get("TagDef.ini"));
        Assert.Equal(["MusicTag", "PictureTag", "ImageTag", "DocumentTag", "WebPageTag"], kinds.Select(k => k.Section));
        var music = kinds[0];
        Assert.Equal("%%Music%%<ID3>", music.Caption);
        Assert.Equal(["mp3", "wma", "ape", "mpc", "mp+", "ogg"], music.Extensions);
        Assert.Contains(music.Fields, f => f.Name == "ID3.Artist" && f.Submenu is null);
        Assert.Contains(music.Fields, f => f.Name == "ID3v2.TALB" && f.Submenu == "v2");
    }

    [Fact]
    public void ReadsId3v1AndV2()
    {
        // v2.3：TIT2（UTF-16 BOM 付き）、TPE1（Latin-1）、TRCK、TCON（番号表記）、COMM
        var frames = new List<byte>();
        void Frame(string id, byte[] body)
        {
            frames.AddRange(Encoding.ASCII.GetBytes(id));
            var size = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(size, body.Length);
            frames.AddRange(size);
            frames.AddRange([0, 0]);
            frames.AddRange(body);
        }
        Frame("TIT2", [1, 0xFF, 0xFE, .. Encoding.Unicode.GetBytes("曲名")]);
        Frame("TPE1", [0, .. Encoding.Latin1.GetBytes("Artist")]);
        Frame("TRCK", [0, .. Encoding.Latin1.GetBytes("3/12")]);
        Frame("TCON", [0, .. Encoding.Latin1.GetBytes("(17)")]);
        Frame("COMM", [0, (byte)'e', (byte)'n', (byte)'g', 0, .. Encoding.Latin1.GetBytes("hello")]);
        var header = new byte[10];
        Encoding.ASCII.GetBytes("ID3").CopyTo(header, 0);
        header[3] = 3;
        int n = frames.Count;
        header[6] = (byte)((n >> 21) & 0x7F);
        header[7] = (byte)((n >> 14) & 0x7F);
        header[8] = (byte)((n >> 7) & 0x7F);
        header[9] = (byte)(n & 0x7F);

        // v1.1：アルバムは v1 にしかない
        var v1 = new byte[128];
        Encoding.ASCII.GetBytes("TAG").CopyTo(v1, 0);
        Encoding.ASCII.GetBytes("OldTitle").CopyTo(v1, 3);
        Encoding.ASCII.GetBytes("OldArtist").CopyTo(v1, 33);
        Encoding.ASCII.GetBytes("Album1").CopyTo(v1, 63);
        Encoding.ASCII.GetBytes("1999").CopyTo(v1, 93);
        v1[126] = 7;
        v1[127] = 17;

        string path = Write("a.mp3", [.. header, .. frames, 0xFF, 0xFB, 0, 0, .. v1]);
        var tags = TagReader.Read(path);
        Assert.Equal("曲名", tags["ID3.Track"].Text);
        Assert.Equal("Artist", tags["ID3.Artist"].Text);
        Assert.Equal("03", tags["ID3.TrackNumber"].Text);
        Assert.Equal("Rock", tags["ID3.Genre"].Text);
        Assert.Equal("hello", tags["ID3v2.COMM"].Text);
        // v2 があるファイルでは v1 を読まない（オリジナル版と同じ。正解データ music-v1）
        Assert.Null(tags["ID3.Album"].Text);
        Assert.Null(tags["ID3v1.Year"].Text);
        Assert.Equal("-1", tags["ID3v1.TrackNumber"].Text);
        Assert.Null(tags["ID3v1.Track"].Text);

        // v1 だけのファイルでは v1 を読み、ID3.* もそこから取る
        string v1Only = Write("b.mp3", [0xFF, 0xFB, 0, 0, .. v1]);
        var v1Tags = TagReader.Read(v1Only);
        Assert.Equal("Album1", v1Tags["ID3.Album"].Text);
        Assert.Equal("1999", v1Tags["ID3v1.Year"].Text);
        Assert.Equal("07", v1Tags["ID3v1.TrackNumber"].Text);
        Assert.Equal("07", v1Tags["ID3.TrackNumber"].Text);
        Assert.Equal("OldTitle", v1Tags["ID3v1.Track"].Text);
        Assert.Equal("Rock", v1Tags["ID3.Genre"].Text);
    }

    [Fact]
    public void ReadsExifFromJpeg()
    {
        // TIFF（リトルエンディアン）に IFD0：Make、DateTimeOriginal（Exif IFD 経由）、Flash
        var tiff = new List<byte>();
        tiff.AddRange("II"u8.ToArray());
        tiff.AddRange([42, 0, 8, 0, 0, 0]);
        byte[] make = Encoding.ASCII.GetBytes("Canon\0");
        // IFD0: 2 entries (Make, ExifIFD) → 2 + 24 + 4 = 30 bytes, starts at 8 → make data at 38, exif IFD at 44
        tiff.AddRange([2, 0]);
        tiff.AddRange(Entry(0x010F, 2, (uint)make.Length, 38));
        tiff.AddRange(Entry(0x8769, 4, 1, 44));
        tiff.AddRange([0, 0, 0, 0]);
        tiff.AddRange(make);
        // Exif IFD at 44: 2 entries (DateTimeOriginal, Flash) → 2 + 24 + 4 = 30 → datetime at 74
        byte[] dt = Encoding.ASCII.GetBytes("2020:05:06 07:08:09\0");
        tiff.AddRange([2, 0]);
        tiff.AddRange(Entry(0x9003, 2, (uint)dt.Length, 74));
        tiff.AddRange(Entry(0x9209, 3, 1, 1));
        tiff.AddRange([0, 0, 0, 0]);
        tiff.AddRange(dt);

        var app1 = new List<byte>();
        app1.AddRange("Exif\0\0"u8.ToArray());
        app1.AddRange(tiff);
        int length = app1.Count + 2;
        string path = Write("p.jpg", [0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. app1, 0xFF, 0xD9]);

        var tags = TagReader.Read(path);
        Assert.Equal("Canon", tags["EXIF.Maker"].Text);
        Assert.Equal(new DateTime(2020, 5, 6, 7, 8, 9), tags["EXIF.OriginalTime"].Time);
        Assert.Equal("0001", tags["EXIF.Flash"].Text);
    }

    private static byte[] Entry(int tag, int type, uint count, uint value)
    {
        var e = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(e, (ushort)tag);
        BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(2), (ushort)type);
        BinaryPrimitives.WriteUInt32LittleEndian(e.AsSpan(4), count);
        if (type == 3)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(8), (ushort)value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(e.AsSpan(8), value);
        }
        return e;
    }

    [Fact]
    public void ReadsHtmlTitle()
    {
        string path = Write("x.html", Encoding.UTF8.GetBytes("<html><head><meta charset=\"utf-8\"><title>  見出し &amp;\n テスト </title></head></html>"));
        Assert.Equal("見出し & テスト", TagReader.Read(path)["HTML.Title"].Text);
    }

    [Fact]
    public void ReadsOoxmlProperties()
    {
        string path = Path.Combine(_dir, "d.docx");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(zip.CreateEntry("docProps/core.xml").Open()))
            {
                w.Write("<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\"><dc:title>T</dc:title><dc:creator>A</dc:creator><dcterms:created>2021-02-03T04:05:06Z</dcterms:created></cp:coreProperties>");
            }
            using (var w = new StreamWriter(zip.CreateEntry("docProps/app.xml").Open()))
            {
                w.Write("<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\"><Application>Word</Application><Company>C</Company></Properties>");
            }
        }
        var tags = TagReader.Read(path);
        Assert.Equal("T", tags["DOC.Title"].Text);
        Assert.Equal("A", tags["DOC.Author"].Text);
        Assert.Equal("Word", tags["DOC.AppName"].Text);
        Assert.Equal("C", tags["DOC.Company"].Text);
        Assert.Equal(new DateTime(2021, 2, 3, 4, 5, 6, DateTimeKind.Utc).ToLocalTime(), tags["DOC.Created"].Time);
    }

    [Fact]
    public void ReadsPdfInfo()
    {
        string path = Write("p.pdf", Encoding.Latin1.GetBytes("%PDF-1.4\n1 0 obj << /Title (Hello \\(World\\)) /Author <FEFF00410042> /CreationDate (D:20200102030405) >> endobj\n%%EOF"));
        var tags = TagReader.Read(path);
        Assert.Equal("Hello (World)", tags["DOC.Title"].Text);
        Assert.Equal("AB", tags["DOC.Author"].Text);
        Assert.Equal(new DateTime(2020, 1, 2, 3, 4, 5), tags["DOC.Created"].Time);
    }

    [Fact]
    public void ReadsOggVorbisComments()
    {
        var body = new List<byte>();
        body.AddRange([3]);
        body.AddRange("vorbis"u8.ToArray());
        byte[] vendor = Encoding.UTF8.GetBytes("v");
        body.AddRange(BitConverter.GetBytes(vendor.Length));
        body.AddRange(vendor);
        string[] comments = ["ARTIST=Someone", "TITLE=Song", "TRACKNUMBER=5"];
        body.AddRange(BitConverter.GetBytes(comments.Length));
        foreach (var c in comments)
        {
            byte[] b = Encoding.UTF8.GetBytes(c);
            body.AddRange(BitConverter.GetBytes(b.Length));
            body.AddRange(b);
        }
        string path = Write("s.ogg", ["O"u8[0], (byte)'g', (byte)'g', (byte)'S', .. new byte[40], .. body]);
        var tags = TagReader.Read(path);
        Assert.Equal("Someone", tags["ID3.Artist"].Text);
        Assert.Equal("Song", tags["ID3.Track"].Text);
        Assert.Equal("05", tags["ID3.TrackNumber"].Text);
    }

    [Fact]
    public void UnreadableFileGivesEmptyTags()
    {
        string path = Write("broken.mp3", [1, 2, 3]);
        // mp3 では v1 がないときのトラック番号 -1 だけが入る（オリジナル版と同じ）
        Assert.Equal(["ID3v1.TrackNumber"], TagReader.Read(path).Values.Keys);
        Assert.Empty(TagReader.Read(Path.Combine(_dir, "missing.jpg")).Values);
    }
}
