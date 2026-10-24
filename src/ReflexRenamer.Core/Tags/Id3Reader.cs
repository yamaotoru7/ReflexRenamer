using System.Globalization;
using System.Text;

namespace ReflexRenamer.Core.Tags;

/// <summary>
/// MP3 の ID3 タグ。v1（末尾 128 バイト）と v2（2.2〜2.4、先頭）を読む。
/// <c>ID3.*</c> は v2 を優先し、なければ v1（ヘルプの仕様）。トラック番号は2桁にする（v7.1 の変更履歴）。
/// </summary>
public static class Id3Reader
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>
    /// ID3v1 のジャンル番号（0〜147）。綴りはオリジナル版の exe に埋め込まれた表に合わせる
    /// （「Phythmic Soul」「A Cappella」「Folk/Rock」「Fast-Fusion」は原文のまま）。
    /// </summary>
    public static readonly string[] Genres =
    [
        "Blues", "Classic Rock", "Country", "Dance", "Disco", "Funk", "Grunge", "Hip-Hop", "Jazz", "Metal", "New Age",
        "Oldies", "Other", "Pop", "R&B", "Rap", "Reggae", "Rock", "Techno", "Industrial", "Alternative", "Ska",
        "Death Metal", "Pranks", "Soundtrack", "Euro-Techno", "Ambient", "Trip-Hop", "Vocal", "Jazz+Funk", "Fusion",
        "Trance", "Classical", "Instrumental", "Acid", "House", "Game", "Sound Clip", "Gospel", "Noise", "AlternRock",
        "Bass", "Soul", "Punk", "Space", "Meditative", "Instrumental Pop", "Instrumental Rock", "Ethnic", "Gothic",
        "Darkwave", "Techno-Industrial", "Electronic", "Pop-Folk", "Eurodance", "Dream", "Southern Rock", "Comedy",
        "Cult", "Gangsta", "Top 40", "Christian Rap", "Pop/Funk", "Jungle", "Native American", "Cabaret", "New Wave",
        "Psychadelic", "Rave", "Showtunes", "Trailer", "Lo-Fi", "Tribal", "Acid Punk", "Acid Jazz", "Polka", "Retro",
        "Musical", "Rock & Roll", "Hard Rock", "Folk", "Folk/Rock", "National Folk", "Swing", "Fast-Fusion", "Bebob",
        "Latin", "Revival", "Celtic", "Bluegrass", "Avantgarde", "Gothic Rock", "Progressive Rock", "Psychedelic Rock",
        "Symphonic Rock", "Slow Rock", "Big Band", "Chorus", "Easy Listening", "Acoustic", "Humour", "Speech",
        "Chanson", "Opera", "Chamber Music", "Sonata", "Symphony", "Booty Bass", "Primus", "Porn Groove", "Satire",
        "Slow Jam", "Club", "Tango", "Samba", "Folklore", "Ballad", "Power Ballad", "Phythmic Soul", "Freestyle",
        "Duet", "Punk Rock", "Drum Solo", "A Cappella", "Euro-House", "Dance Hall", "Goa", "Drum & Bass", "Club-House",
        "Hardcore", "Terror", "Indie", "BritPop", "Negerpunk", "Polsk Punk", "Beat", "Christian Gangsta Rap", "Heavy Metal",
        "Black Metal", "Crossover", "Contemporary Christian", "Christian Rock", "Merengue", "Salsa", "Thrash Metal",
        "Anime", "JPop", "Synthpop",
    ];

    /// <summary>番号で表せないジャンル（範囲外の番号や、番号でない文字列）の表示。オリジナル版と同じ。</summary>
    public const string OtherGenre = "(Other)";

    public static TagSet Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var tags = new TagSet();
        // オリジナル版は v2 があるファイルでは v1 を読まない（正解データ music-v1：v1 の欄が空、TrackNumber が -1）
        if (!ReadV2(stream, tags))
        {
            ReadV1(stream, tags);
        }
        tags.SetIfMissing("ID3v1.TrackNumber", "-1");
        return tags;
    }

    private static void ReadV1(Stream stream, TagSet tags)
    {
        if (stream.Length < 128)
        {
            return;
        }
        stream.Seek(-128, SeekOrigin.End);
        var b = new byte[128];
        stream.ReadExactly(b);
        if (b[0] != 'T' || b[1] != 'A' || b[2] != 'G')
        {
            return;
        }
        var text = TextEncodingForV1();
        string Field(int offset, int length) => text.GetString(b, offset, length).TrimEnd('\0', ' ');
        tags.Set("ID3v1.Track", Field(3, 30));
        tags.Set("ID3v1.Artist", Field(33, 30));
        tags.Set("ID3v1.Album", Field(63, 30));
        tags.Set("ID3v1.Year", Field(93, 4));
        // v1.1 ではコメントの末尾2バイトが 0 とトラック番号
        bool v11 = b[125] == 0 && b[126] != 0;
        tags.Set("ID3v1.Comment", Field(97, v11 ? 28 : 30));
        if (v11)
        {
            tags.Set("ID3v1.TrackNumber", b[126].ToString("00", CultureInfo.InvariantCulture));
        }
        if (b[127] < Genres.Length)
        {
            tags.Set("ID3v1.Genre", Genres[b[127]]);
        }
        else
        {
            tags.Set("ID3v1.Genre", OtherGenre);
        }
        foreach (var name in new[] { "Track", "Artist", "Album", "Year", "Comment", "TrackNumber", "Genre" })
        {
            tags.SetIfMissing("ID3." + name, tags["ID3v1." + name]);
        }
    }

    /// <summary>ID3v1 は文字コードが決まっていないので、システムの ANSI コードページで読む（日本語環境なら Shift-JIS）。</summary>
    private static Encoding TextEncodingForV1()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            return Latin1;
        }
    }

    /// <summary>ID3v2 を読む。v2 のヘッダがなければ false。</summary>
    private static bool ReadV2(Stream stream, TagSet tags)
    {
        stream.Position = 0;
        var header = new byte[10];
        if (stream.Read(header, 0, 10) < 10 || header[0] != 'I' || header[1] != 'D' || header[2] != '3')
        {
            return false;
        }
        int version = header[3];
        bool unsync = (header[5] & 0x80) != 0;
        bool extended = (header[5] & 0x40) != 0;
        int size = SyncSafe(header, 6);
        var body = new byte[size];
        int read = stream.Read(body, 0, size);
        if (read < size)
        {
            Array.Resize(ref body, read);
        }
        if (unsync && version < 4)
        {
            body = Unsynchronize(body);
        }

        int pos = 0;
        if (extended)
        {
            int extSize = version == 4 ? SyncSafe(body, 0) : BigEndian(body, 0);
            pos += extSize + (version == 4 ? 0 : 4);
        }

        int headerSize = version == 2 ? 6 : 10;
        while (pos + headerSize <= body.Length)
        {
            string id = Latin1.GetString(body, pos, version == 2 ? 3 : 4);
            if (id[0] == '\0')
            {
                break;
            }
            int frameSize = version switch
            {
                2 => (body[pos + 3] << 16) | (body[pos + 4] << 8) | body[pos + 5],
                4 => SyncSafe(body, pos + 4),
                _ => BigEndian(body, pos + 4),
            };
            int flags = version == 2 ? 0 : (body[pos + 8] << 8) | body[pos + 9];
            pos += headerSize;
            if (frameSize <= 0 || pos + frameSize > body.Length)
            {
                break;
            }
            var frame = new ReadOnlySpan<byte>(body, pos, frameSize);
            if (version == 4 && (flags & 0x02) != 0)
            {
                frame = Unsynchronize(frame.ToArray());
            }
            if (version == 4 && (flags & 0x01) != 0 && frame.Length >= 4)
            {
                frame = frame[4..]; // データ長表示を飛ばす
            }
            pos += frameSize;

            string id4 = version == 2 ? V22ToV23(id) : id;
            if (id4.Length == 0)
            {
                continue;
            }
            if (id4 == "COMM")
            {
                tags.SetIfMissing("ID3v2.COMM", ReadComment(frame));
            }
            else if (id4 == "TXXX" || id4 == "WXXX")
            {
                tags.SetIfMissing("ID3v2." + id4, ReadUserText(frame, id4 == "WXXX"));
            }
            else if (id4[0] == 'T')
            {
                tags.SetIfMissing("ID3v2." + id4, ReadText(frame));
            }
            else if (id4[0] == 'W')
            {
                tags.SetIfMissing("ID3v2." + id4, Latin1.GetString(frame).TrimEnd('\0'));
            }
        }

        // 論理タグ（ID3.*）：v2.4 の TDRC は TYER の代わり
        tags.SetIfMissing("ID3.Artist", tags["ID3v2.TPE1"]);
        tags.SetIfMissing("ID3.Album", tags["ID3v2.TALB"]);
        tags.SetIfMissing("ID3.Track", tags["ID3v2.TIT2"]);
        tags.SetIfMissing("ID3.Year", tags["ID3v2.TYER"].IsEmpty ? tags["ID3v2.TDRC"] : tags["ID3v2.TYER"]);
        tags.SetIfMissing("ID3.Comment", tags["ID3v2.COMM"]);
        tags.SetIfMissing("ID3.Genre", NormalizeGenre(tags["ID3v2.TCON"].Text));
        tags.SetIfMissing("ID3.TrackNumber", NormalizeTrackNumber(tags["ID3v2.TRCK"].Text));
        // <ID3v2.TCON> も番号なら名前にする（文字列ならそのまま。正解データ music-v2）
        if (GenreByNumber(tags["ID3v2.TCON"].Text) is { } genreName)
        {
            tags.Set("ID3v2.TCON", genreName);
        }
        return true;
    }

    /// <summary>「(17)」や「17」のような番号表記のジャンルを名前にする。番号でなければ null。</summary>
    private static string? GenreByNumber(string? genre)
    {
        if (string.IsNullOrEmpty(genre))
        {
            return null;
        }
        string digits = genre.Trim('(', ')');
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0 && n < Genres.Length ? Genres[n] : null;
    }

    /// <summary>
    /// <c>&lt;ID3.Genre&gt;</c> の値。オリジナル版は番号だけを名前にし、「Jazz」のような文字列や範囲外の番号は「(Other)」にする（正解データ music-all）。
    /// </summary>
    private static string? NormalizeGenre(string? genre) =>
        string.IsNullOrEmpty(genre) ? null : GenreByNumber(genre) ?? OtherGenre;

    /// <summary>「3/12」の「3」を2桁にする。</summary>
    public static string? NormalizeTrackNumber(string? track)
    {
        if (track is null)
        {
            return null;
        }
        string first = track.Split('/')[0].Trim();
        return int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n.ToString("00", CultureInfo.InvariantCulture) : track;
    }

    private static string V22ToV23(string id) => id switch
    {
        "TT2" => "TIT2", "TP1" => "TPE1", "TAL" => "TALB", "TYE" => "TYER", "TCO" => "TCON", "TRK" => "TRCK", "COM" => "COMM",
        "TT1" => "TIT1", "TT3" => "TIT3", "TP2" => "TPE2", "TP3" => "TPE3", "TP4" => "TPE4", "TCM" => "TCOM", "TXT" => "TEXT",
        "TPA" => "TPOS", "TPB" => "TPUB", "TEN" => "TENC", "TLA" => "TLAN", "TLE" => "TLEN", "TKE" => "TKEY", "TBP" => "TBPM",
        "TCR" => "TCOP", "TOA" => "TOPE", "TOT" => "TOAL", "TOF" => "TOFN", "TOL" => "TOLY", "TOR" => "TORY", "TSI" => "TSIZ",
        "TSS" => "TSSE", "TRC" => "TSRC", "TXX" => "TXXX", "WXX" => "WXXX", "WAF" => "WOAF", "WAR" => "WOAR", "WAS" => "WOAS", "WCP" => "WCOP",
        _ => "",
    };

    private static string ReadText(ReadOnlySpan<byte> frame)
    {
        if (frame.Length == 0)
        {
            return "";
        }
        return Decode(frame[0], frame[1..]).TrimEnd('\0');
    }

    private static string ReadUserText(ReadOnlySpan<byte> frame, bool urlValue)
    {
        if (frame.Length == 0)
        {
            return "";
        }
        int split = TerminatorIndex(frame[0], frame[1..]);
        string description = split < 0 ? "" : Decode(frame[0], frame.Slice(1, split));
        var rest = split < 0 ? frame[1..] : frame[(1 + split + TerminatorLength(frame[0]))..];
        string value = urlValue ? Latin1.GetString(rest).TrimEnd('\0') : Decode(frame[0], rest).TrimEnd('\0');
        return description.Length > 0 ? description + ": " + value : value;
    }

    private static string ReadComment(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 4)
        {
            return "";
        }
        int split = TerminatorIndex(frame[0], frame[4..]);
        var rest = split < 0 ? frame[4..] : frame[(4 + split + TerminatorLength(frame[0]))..];
        return Decode(frame[0], rest).TrimEnd('\0');
    }

    /// <summary>文字コードに応じた終端（0 または 00）の位置。なければ -1。</summary>
    private static int TerminatorIndex(byte encoding, ReadOnlySpan<byte> data)
    {
        bool wide = encoding is 1 or 2;
        for (int i = 0; i + (wide ? 1 : 0) < data.Length; i += wide ? 2 : 1)
        {
            if (data[i] == 0 && (!wide || data[i + 1] == 0))
            {
                return i;
            }
        }
        return -1;
    }

    private static int TerminatorLength(byte encoding) => encoding is 1 or 2 ? 2 : 1;

    private static string Decode(byte encoding, ReadOnlySpan<byte> data) => encoding switch
    {
        1 => DecodeUtf16WithBom(data),
        2 => Encoding.BigEndianUnicode.GetString(data),
        3 => Encoding.UTF8.GetString(data),
        _ => TextEncodingForV1().GetString(data),
    };

    private static string DecodeUtf16WithBom(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(data[2..]);
        }
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(data[2..]);
        }
        return Encoding.Unicode.GetString(data);
    }

    private static int SyncSafe(byte[] b, int i) => (b[i] << 21) | (b[i + 1] << 14) | (b[i + 2] << 7) | b[i + 3];

    private static int BigEndian(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

    private static byte[] Unsynchronize(byte[] data)
    {
        var result = new List<byte>(data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            result.Add(data[i]);
            if (data[i] == 0xFF && i + 1 < data.Length && data[i + 1] == 0x00)
            {
                i++;
            }
        }
        return result.ToArray();
    }
}
