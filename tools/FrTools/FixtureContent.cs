using System.Buffers.Binary;
using System.Text;

namespace FrTools;

/// <summary>タグ付きのテスト用ファイルの中身を作る（タグ・リネームの観察用）。</summary>
internal static class FixtureContent
{
    /// <summary>ID3v2.3（UTF-16 のタイトル、Latin-1 のアーティストなど）と ID3v1.1 を持つ MP3 もどき。</summary>
    public static byte[] Mp3(string title, string artist, string album, string year, int track, string genre, string comment, bool unicode = true, bool withV2 = true, bool withV1 = true)
    {
        // テキストフレーム：unicode なら UTF-16（BOM 付き、encoding=1）、そうでなければ ANSI（CP932、encoding=0）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansi = Encoding.GetEncoding(932);
        byte[] Text(string text) => unicode ? [1, 0xFF, 0xFE, .. Encoding.Unicode.GetBytes(text)] : [0, .. ansi.GetBytes(text)];
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
        Frame("TIT2", Text(title));
        Frame("TPE1", Text(artist));
        Frame("TALB", Text(album));
        Frame("TYER", [0, .. Encoding.Latin1.GetBytes(year)]);
        Frame("TRCK", [0, .. Encoding.Latin1.GetBytes(track.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        Frame("TCON", [0, .. Encoding.Latin1.GetBytes(genre)]);
        Frame("COMM", [1, (byte)'e', (byte)'n', (byte)'g', 0xFF, 0xFE, 0, 0, 0xFF, 0xFE, .. Encoding.Unicode.GetBytes(comment)]);
        var header = new byte[10];
        Encoding.ASCII.GetBytes("ID3").CopyTo(header, 0);
        header[3] = 3;
        int n = frames.Count;
        header[6] = (byte)((n >> 21) & 0x7F);
        header[7] = (byte)((n >> 14) & 0x7F);
        header[8] = (byte)((n >> 7) & 0x7F);
        header[9] = (byte)(n & 0x7F);

        var v1 = new byte[128];
        Encoding.ASCII.GetBytes("TAG").CopyTo(v1, 0);
        Encoding.ASCII.GetBytes("V1 " + Ascii(title)).CopyTo(v1, 3);
        Encoding.ASCII.GetBytes("V1 " + Ascii(artist)).CopyTo(v1, 33);
        Encoding.ASCII.GetBytes("V1 " + Ascii(album)).CopyTo(v1, 63);
        Encoding.ASCII.GetBytes("1990").CopyTo(v1, 93);
        v1[126] = (byte)(track + 10);
        v1[127] = 17;

        // MPEG フレームヘッダらしきもの（無音の代わり）
        var audio = new byte[417];
        audio[0] = 0xFF;
        audio[1] = 0xFB;
        audio[2] = 0x90;
        return [.. (withV2 ? [.. header, .. frames] : Array.Empty<byte>()), .. audio, .. (withV1 ? v1 : Array.Empty<byte>())];
    }

    private static string Ascii(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            sb.Append(c < 128 ? c : '?');
        }
        return sb.ToString()[..Math.Min(sb.Length, 27)];
    }

    /// <summary>EXIF（メーカー、モデル、撮影日時、フラッシュ、露出時間、ISO）を持つ最小の JPEG。</summary>
    public static byte[] Jpeg(string make, string model, string dateTime, int flash, (uint, uint) exposure, int iso)
    {
        var entries0 = new List<(int Tag, int Type, byte[] Data, uint Inline)>
        {
            (0x010F, 2, Encoding.ASCII.GetBytes(make + "\0"), 0),
            (0x0110, 2, Encoding.ASCII.GetBytes(model + "\0"), 0),
        };
        var entriesExif = new List<(int Tag, int Type, byte[] Data, uint Inline)>
        {
            (0x829A, 5, Rational(exposure.Item1, exposure.Item2), 0),
            (0x8827, 3, [], (uint)iso),
            (0x9003, 2, Encoding.ASCII.GetBytes(dateTime + "\0"), 0),
            (0x9209, 3, [], (uint)flash),
        };

        var tiff = new List<byte>();
        tiff.AddRange("II"u8.ToArray());
        tiff.AddRange([42, 0, 8, 0, 0, 0]);
        // IFD0 は 8 から。エントリは Make, Model, ExifIFD の 3 つ
        int ifd0Size = 2 + 3 * 12 + 4;
        int dataStart = 8 + ifd0Size;
        var data0 = new List<byte>();
        tiff.AddRange([3, 0]);
        foreach (var e in entries0)
        {
            tiff.AddRange(Entry(e.Tag, e.Type, (uint)e.Data.Length, (uint)(dataStart + data0.Count)));
            data0.AddRange(e.Data);
            if (data0.Count % 2 == 1) data0.Add(0);
        }
        int exifIfd = dataStart + data0.Count;
        tiff.AddRange(Entry(0x8769, 4, 1, (uint)exifIfd));
        tiff.AddRange([0, 0, 0, 0]);
        tiff.AddRange(data0);
        // Exif IFD
        int exifSize = 2 + entriesExif.Count * 12 + 4;
        int exifData = exifIfd + exifSize;
        var dataExif = new List<byte>();
        tiff.AddRange([(byte)entriesExif.Count, 0]);
        foreach (var e in entriesExif)
        {
            if (e.Data.Length == 0)
            {
                tiff.AddRange(Entry(e.Tag, e.Type, 1, e.Inline));
            }
            else
            {
                uint count = e.Type == 5 ? 1u : (uint)e.Data.Length;
                tiff.AddRange(Entry(e.Tag, e.Type, count, (uint)(exifData + dataExif.Count)));
                dataExif.AddRange(e.Data);
                if (dataExif.Count % 2 == 1) dataExif.Add(0);
            }
        }
        tiff.AddRange([0, 0, 0, 0]);
        tiff.AddRange(dataExif);

        var app1 = new List<byte>();
        app1.AddRange("Exif\0\0"u8.ToArray());
        app1.AddRange(tiff);
        int length = app1.Count + 2;
        // 最小の画像データ（1x1 のグレー JPEG）
        byte[] image =
        [
            0xFF, 0xDB, 0x00, 0x43, 0x00, .. Enumerable.Repeat((byte)1, 64),
            0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
            0xFF, 0xC4, 0x00, 0x1F, 0x00, 0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B,
            0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00, 0x00,
            0xFF, 0xD9,
        ];
        return [0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. app1, .. image];
    }

    private static byte[] Rational(uint num, uint den)
    {
        var b = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(b, num);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), den);
        return b;
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

    public static byte[] Html(string title) =>
        Encoding.UTF8.GetBytes($"<html><head><meta charset=\"utf-8\"><title>{title}</title></head><body></body></html>");

    public static byte[] Pdf(string title, string author) =>
        Encoding.Latin1.GetBytes($"%PDF-1.4\n1 0 obj << /Title ({title}) /Author ({author}) /CreationDate (D:20200102030405) >> endobj\ntrailer << /Info 1 0 R >>\n%%EOF");

    // ---- WMA（ASF）、APEv2、IPTC：仕様どおりの最小構成（T402、T404 でオリジナル版に読ませる） ----

    private static readonly Guid AsfHeader = new("75B22630-668E-11CF-A6D9-00AA0062CE6C");
    private static readonly Guid AsfContentDescription = new("75B22633-668E-11CF-A6D9-00AA0062CE6C");
    private static readonly Guid AsfExtendedContentDescription = new("D2D0A440-E307-11D2-97F0-00A0C95EA850");

    /// <summary>ASF の Header Object に Content Description（タイトル・作者・コメント）と Extended Content Description を入れた WMA もどき。</summary>
    public static byte[] Wma(string title, string author, string comment, params (string Name, object Value)[] extended)
    {
        static byte[] Str(string v) => Encoding.Unicode.GetBytes(v + "\0");
        byte[] t = Str(title), a = Str(author), c = Str(""), d = Str(comment), r = Str("");
        var cd = new List<byte>();
        cd.AddRange(AsfContentDescription.ToByteArray());
        cd.AddRange(BitConverter.GetBytes((long)(24 + 10 + t.Length + a.Length + c.Length + d.Length + r.Length)));
        foreach (var part in new[] { t, a, c, d, r }) cd.AddRange(BitConverter.GetBytes((ushort)part.Length));
        foreach (var part in new[] { t, a, c, d, r }) cd.AddRange(part);

        var entries = new List<byte>();
        foreach (var (name, value) in extended)
        {
            byte[] n = Str(name);
            (ushort type, byte[] v) = value switch
            {
                uint u => ((ushort)3, BitConverter.GetBytes(u)),
                _ => ((ushort)0, Str(value.ToString()!)),
            };
            entries.AddRange(BitConverter.GetBytes((ushort)n.Length));
            entries.AddRange(n);
            entries.AddRange(BitConverter.GetBytes(type));
            entries.AddRange(BitConverter.GetBytes((ushort)v.Length));
            entries.AddRange(v);
        }
        var ecd = new List<byte>();
        ecd.AddRange(AsfExtendedContentDescription.ToByteArray());
        ecd.AddRange(BitConverter.GetBytes((long)(24 + 2 + entries.Count)));
        ecd.AddRange(BitConverter.GetBytes((ushort)extended.Length));
        ecd.AddRange(entries);

        var file = new List<byte>();
        file.AddRange(AsfHeader.ToByteArray());
        file.AddRange(BitConverter.GetBytes((long)(30 + cd.Count + ecd.Count)));
        file.AddRange(BitConverter.GetBytes(2));
        file.Add(1);
        file.Add(2);
        file.AddRange(cd);
        file.AddRange(ecd);
        file.AddRange(new byte[64]);
        return file.ToArray();
    }

    /// <summary>音声データの後ろに APEv2 タグ（UTF-8）を付けたもの。header でヘッダも付ける。</summary>
    public static byte[] Ape(bool header, params (string Key, string Value)[] items)
    {
        var body = new List<byte>();
        foreach (var (key, value) in items)
        {
            byte[] v = Encoding.UTF8.GetBytes(value);
            body.AddRange(BitConverter.GetBytes(v.Length));
            body.AddRange(BitConverter.GetBytes(0));
            body.AddRange(Encoding.ASCII.GetBytes(key));
            body.Add(0);
            body.AddRange(v);
        }
        byte[] Block(bool isHeader)
        {
            var b = new byte[32];
            Encoding.ASCII.GetBytes("APETAGEX").CopyTo(b, 0);
            BitConverter.GetBytes(2000).CopyTo(b, 8);
            BitConverter.GetBytes(body.Count + 32).CopyTo(b, 12);
            BitConverter.GetBytes(items.Length).CopyTo(b, 16);
            uint flags = (header ? 0x80000000u : 0) | (isHeader ? 0x20000000u : 0);
            BitConverter.GetBytes(flags).CopyTo(b, 20);
            return b;
        }
        var file = new List<byte>(new byte[64]);
        if (header) file.AddRange(Block(true));
        file.AddRange(body);
        file.AddRange(Block(false));
        return file.ToArray();
    }

    /// <summary>IPTC-IIM（レコード 2）のデータセットを、JPEG の APP13（Photoshop 3.0、8BIM 0x0404）に入れたもの。文字は ASCII。</summary>
    public static byte[] JpegIptc(params (byte DataSet, string Value)[] dataSets) => Jpeg13(Iim(dataSets));

    /// <summary>同じ IPTC を PSD の画像リソースに入れたもの。</summary>
    public static byte[] PsdIptc(params (byte DataSet, string Value)[] dataSets)
    {
        byte[] bim = Bim(Iim(dataSets));
        var file = new List<byte>();
        file.AddRange(Encoding.ASCII.GetBytes("8BPS"));
        file.AddRange(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, 1, 0, 8, 0, 3 });
        file.AddRange(new byte[] { 0, 0, 0, 0 });
        file.AddRange(BigEndian((uint)bim.Length));
        file.AddRange(bim);
        file.AddRange(new byte[] { 0, 0, 0, 0 });
        file.AddRange(new byte[] { 0, 0, 0, 0, 0, 0 });
        return file.ToArray();
    }

    private static byte[] Iim((byte DataSet, string Value)[] dataSets)
    {
        var iim = new List<byte>();
        foreach (var (ds, value) in dataSets)
        {
            byte[] v = Encoding.ASCII.GetBytes(value);
            iim.AddRange(new byte[] { 0x1C, 2, ds, (byte)(v.Length >> 8), (byte)v.Length });
            iim.AddRange(v);
        }
        return iim.ToArray();
    }

    private static byte[] Bim(byte[] iim)
    {
        var bim = new List<byte>();
        bim.AddRange(Encoding.ASCII.GetBytes("8BIM"));
        bim.AddRange(new byte[] { 0x04, 0x04, 0, 0 });
        bim.AddRange(BigEndian((uint)iim.Length));
        bim.AddRange(iim);
        if (iim.Length % 2 == 1) bim.Add(0);
        return bim.ToArray();
    }

    private static byte[] Jpeg13(byte[] iim)
    {
        var payload = new List<byte>();
        payload.AddRange(Encoding.ASCII.GetBytes("Photoshop 3.0\0"));
        payload.AddRange(Bim(iim));
        int len = payload.Count + 2;
        var file = new List<byte> { 0xFF, 0xD8, 0xFF, 0xED, (byte)(len >> 8), (byte)len };
        file.AddRange(payload);
        file.Add(0xFF);
        file.Add(0xD9);
        return file.ToArray();
    }

    /// <summary>既存の JPEG の SOI と最初のセグメント（APP0/APP1）の後に、IPTC の APP13 を差し込む。</summary>
    public static byte[] InsertIptc(byte[] jpeg, params (byte DataSet, string Value)[] dataSets)
    {
        byte[] app13 = Jpeg13(Iim(dataSets))[2..^2];
        int firstLength = (jpeg[4] << 8) | jpeg[5];
        int at = 4 + firstLength;
        return [.. jpeg[..at], .. app13, .. jpeg[at..]];
    }

    /// <summary>System.Drawing で作った本物の（デコードできる）8x8 の JPEG。</summary>
    public static byte[] RealJpeg()
    {
        using var bitmap = new System.Drawing.Bitmap(8, 8);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.SteelBlue);
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
        return ms.ToArray();
    }

    /// <summary>Ogg Vorbis の識別ヘッダとコメントヘッダ（ページごとに CRC 付き）。コメントは「KEY=値」。</summary>
    public static byte[] Ogg(params string[] comments)
    {
        var ident = new List<byte> { 1 };
        ident.AddRange(Encoding.ASCII.GetBytes("vorbis"));
        ident.AddRange(BitConverter.GetBytes(0));        // version
        ident.Add(2);                                     // channels
        ident.AddRange(BitConverter.GetBytes(44100));     // sample rate
        ident.AddRange(BitConverter.GetBytes(0));
        ident.AddRange(BitConverter.GetBytes(128000));
        ident.AddRange(BitConverter.GetBytes(0));
        ident.Add(0xB8);                                  // blocksize 256/2048
        ident.Add(1);                                     // framing

        var comment = new List<byte> { 3 };
        comment.AddRange(Encoding.ASCII.GetBytes("vorbis"));
        byte[] vendor = Encoding.UTF8.GetBytes("FrTools");
        comment.AddRange(BitConverter.GetBytes(vendor.Length));
        comment.AddRange(vendor);
        comment.AddRange(BitConverter.GetBytes(comments.Length));
        foreach (var c in comments)
        {
            byte[] b = Encoding.UTF8.GetBytes(c);
            comment.AddRange(BitConverter.GetBytes(b.Length));
            comment.AddRange(b);
        }
        comment.Add(1);

        // 実際のファイルと同じく、2 ページ目にコメントヘッダとセットアップヘッダ（vorbis）を入れ、音声のページを続ける
        var setup = new List<byte> { 5 };
        setup.AddRange(Encoding.ASCII.GetBytes("vorbis"));
        setup.AddRange(new byte[32]);
        var file = new List<byte>();
        file.AddRange(OggPage([ident.ToArray()], 0, 2, 0));
        file.AddRange(OggPage([comment.ToArray(), setup.ToArray()], 1, 0, 0));
        file.AddRange(OggPage([new byte[100]], 2, 4, 4096));
        return file.ToArray();
    }

    private static byte[] OggPage(byte[][] packets, uint sequence, byte headerType, long granule)
    {
        var segments = new List<byte>();
        foreach (var packet in packets)
        {
            int left = packet.Length;
            while (left >= 255)
            {
                segments.Add(255);
                left -= 255;
            }
            segments.Add((byte)left);
        }
        var page = new List<byte>();
        page.AddRange(Encoding.ASCII.GetBytes("OggS"));
        page.Add(0);
        page.Add(headerType);
        page.AddRange(BitConverter.GetBytes(granule));
        page.AddRange(BitConverter.GetBytes(0x46725431u));
        page.AddRange(BitConverter.GetBytes(sequence));
        page.AddRange(BitConverter.GetBytes(0u));
        page.Add((byte)segments.Count);
        page.AddRange(segments);
        foreach (var packet in packets) page.AddRange(packet);
        byte[] bytes = page.ToArray();
        BitConverter.GetBytes(OggCrc(bytes)).CopyTo(bytes, 22);
        return bytes;
    }

    private static uint OggCrc(byte[] data)
    {
        uint crc = 0;
        foreach (byte b in data)
        {
            crc ^= (uint)b << 24;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
            }
        }
        return crc;
    }

    /// <summary>xref と trailer を正しく持つ 1 ページの PDF（文書情報 /Info 付き）。</summary>
    public static byte[] WellFormedPdf(string title, string author, string subject, string keywords, string creator, string producer, string created, string modified)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << >> >>",
            $"<< /Title ({title}) /Author ({author}) /Subject ({subject}) /Keywords ({keywords}) /Creator ({creator}) /Producer ({producer}) /CreationDate ({created}) /ModDate ({modified}) >>",
        };
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        int xref = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (int o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Info 4 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    private static byte[] BigEndian(uint v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
}
