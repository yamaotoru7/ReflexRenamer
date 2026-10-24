using System.Buffers.Binary;
using System.Text;

namespace ReflexRenamer.Core.Tags;

/// <summary>WMA（ASF）のコンテンツ記述と拡張コンテンツ記述。<c>ID3.*</c> に対応付ける。</summary>
public static class AsfReader
{
    private static readonly Guid HeaderObject = new("75B22630-668E-11CF-A6D9-00AA0062CE6C");
    private static readonly Guid ContentDescription = new("75B22633-668E-11CF-A6D9-00AA0062CE6C");
    private static readonly Guid ExtendedContentDescription = new("D2D0A440-E307-11D2-97F0-00A0C95EA850");

    public static TagSet Read(string path)
    {
        var tags = new TagSet();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = new byte[30];
        if (stream.Read(header, 0, 30) < 30 || new Guid(header.AsSpan(0, 16)) != HeaderObject)
        {
            return tags;
        }
        long headerSize = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(16));
        var body = new byte[Math.Min(headerSize - 30, 8 * 1024 * 1024)];
        int read = stream.Read(body, 0, body.Length);
        int pos = 0;
        while (pos + 24 <= read)
        {
            var guid = new Guid(body.AsSpan(pos, 16));
            long size = BinaryPrimitives.ReadInt64LittleEndian(body.AsSpan(pos + 16));
            if (size < 24 || pos + size > read)
            {
                break;
            }
            var data = body.AsSpan(pos + 24, (int)size - 24);
            if (guid == ContentDescription)
            {
                ReadContentDescription(data, tags);
            }
            else if (guid == ExtendedContentDescription)
            {
                ReadExtended(data, tags);
            }
            pos += (int)size;
        }
        return tags;
    }

    private static void ReadContentDescription(ReadOnlySpan<byte> d, TagSet tags)
    {
        if (d.Length < 10)
        {
            return;
        }
        var lengths = new int[5];
        for (int i = 0; i < 5; i++)
        {
            lengths[i] = BinaryPrimitives.ReadUInt16LittleEndian(d[(i * 2)..]);
        }
        int pos = 10;
        string[] names = ["Title", "Author", "Copyright", "Description", "Rating"];
        for (int i = 0; i < 5 && pos + lengths[i] <= d.Length; i++)
        {
            string value = Encoding.Unicode.GetString(d.Slice(pos, lengths[i])).TrimEnd('\0');
            tags.Set("WMA." + names[i], value);
            pos += lengths[i];
        }
        tags.SetIfMissing("ID3.Track", tags["WMA.Title"]);
        tags.SetIfMissing("ID3.Artist", tags["WMA.Author"]);
        tags.SetIfMissing("ID3.Comment", tags["WMA.Description"]);
    }

    private static void ReadExtended(ReadOnlySpan<byte> d, TagSet tags)
    {
        // 曲番号は数値（DWORD）のときだけ使う。WM/TrackNumber は 1 から、WM/Track は 0 から数える
        // （オリジナル版の実測：golden tagrename-formats。文字列の WM/TrackNumber は -1 になる）
        long? trackNumber = null, track = null;
        int count = BinaryPrimitives.ReadUInt16LittleEndian(d);
        int pos = 2;
        for (int i = 0; i < count && pos + 6 <= d.Length; i++)
        {
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(d[pos..]);
            pos += 2;
            string name = Encoding.Unicode.GetString(d.Slice(pos, nameLength)).TrimEnd('\0');
            pos += nameLength;
            int type = BinaryPrimitives.ReadUInt16LittleEndian(d[pos..]);
            int length = BinaryPrimitives.ReadUInt16LittleEndian(d[(pos + 2)..]);
            pos += 4;
            if (pos + length > d.Length)
            {
                break;
            }
            var value = d.Slice(pos, length);
            pos += length;
            string text = type switch
            {
                0 => Encoding.Unicode.GetString(value).TrimEnd('\0'),
                2 or 3 => BinaryPrimitives.ReadUInt32LittleEndian(value).ToString(System.Globalization.CultureInfo.InvariantCulture),
                4 => BinaryPrimitives.ReadUInt64LittleEndian(value).ToString(System.Globalization.CultureInfo.InvariantCulture),
                5 => BinaryPrimitives.ReadUInt16LittleEndian(value).ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => "",
            };
            tags.Set("WMA." + name, text);
            if (type is 2 or 3 && length >= 4)
            {
                long number = BinaryPrimitives.ReadUInt32LittleEndian(value);
                if (name == "WM/TrackNumber") trackNumber = number;
                else if (name == "WM/Track") track = number + 1;
            }
        }
        tags.SetIfMissing("ID3.Album", tags["WMA.WM/AlbumTitle"]);
        tags.SetIfMissing("ID3.Year", tags["WMA.WM/Year"]);
        tags.SetIfMissing("ID3.Genre", tags["WMA.WM/Genre"]);
        tags.SetIfMissing("ID3.TrackNumber", (trackNumber ?? track) is { } n
            ? Id3Reader.NormalizeTrackNumber(n.ToString(System.Globalization.CultureInfo.InvariantCulture))
            : "-1");
        tags.SetIfMissing("ID3.Artist", tags["WMA.WM/AlbumArtist"]);
        AudioTags.CopyToId3v1(tags);
    }
}

/// <summary>APEv2 タグ（APE、Musepack）。ファイル末尾（ID3v1 の前）にある。</summary>
public static class ApeReader
{
    public static TagSet Read(string path)
    {
        var tags = new TagSet();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long end = stream.Length;
        // 末尾に ID3v1 があればその前を見る
        if (end >= 128)
        {
            stream.Seek(-128, SeekOrigin.End);
            var v1 = new byte[3];
            stream.ReadExactly(v1);
            if (v1[0] == 'T' && v1[1] == 'A' && v1[2] == 'G')
            {
                end -= 128;
            }
        }
        if (end < 32)
        {
            return tags;
        }
        stream.Position = end - 32;
        var footer = new byte[32];
        stream.ReadExactly(footer);
        if (Encoding.ASCII.GetString(footer, 0, 8) != "APETAGEX")
        {
            return tags;
        }
        int size = BinaryPrimitives.ReadInt32LittleEndian(footer.AsSpan(12));
        int count = BinaryPrimitives.ReadInt32LittleEndian(footer.AsSpan(16));
        if (size < 32 || size > end)
        {
            return tags;
        }
        stream.Position = end - size;
        var body = new byte[size - 32];
        stream.ReadExactly(body);
        int pos = 0;
        for (int i = 0; i < count && pos + 8 <= body.Length; i++)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(pos));
            int flags = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(pos + 4));
            pos += 8;
            int keyEnd = Array.IndexOf(body, (byte)0, pos);
            if (keyEnd < 0 || keyEnd + 1 + length > body.Length)
            {
                break;
            }
            string key = Encoding.ASCII.GetString(body, pos, keyEnd - pos);
            pos = keyEnd + 1;
            if ((flags & 0x6) == 0)
            {
                tags.Set("APE." + key, Encoding.UTF8.GetString(body, pos, length).Replace('\0', ';'));
            }
            pos += length;
        }
        tags.SetIfMissing("ID3.Artist", tags["APE.Artist"]);
        tags.SetIfMissing("ID3.Album", tags["APE.Album"]);
        tags.SetIfMissing("ID3.Track", tags["APE.Title"]);
        tags.SetIfMissing("ID3.Year", tags["APE.Year"]);
        tags.SetIfMissing("ID3.Comment", tags["APE.Comment"]);
        // ジャンルがなければ ID3v1 と同じく「(Other)」（オリジナル版の実測：golden tagrename-formats）
        tags.SetIfMissing("ID3.Genre", tags["APE.Genre"].Text ?? "(Other)");
        tags.SetIfMissing("ID3.TrackNumber", tags["APE.Track"].Text is { } apeTrack ? Id3Reader.NormalizeTrackNumber(apeTrack) : "00");
        AudioTags.CopyToId3v1(tags);
        return tags;
    }
}

internal static class AudioTags
{
    private static readonly string[] Id3v1Names = ["Artist", "Album", "Track", "TrackNumber", "Year", "Comment", "Genre"];

    /// <summary>WMA・APE・OGG では <c>ID3v1.*</c> も <c>ID3.*</c> と同じ値になる（オリジナル版の実測）。</summary>
    public static void CopyToId3v1(TagSet tags)
    {
        foreach (var name in Id3v1Names)
        {
            tags.SetIfMissing("ID3v1." + name, tags["ID3." + name]);
        }
    }
}

/// <summary>Ogg Vorbis のコメント（先頭のページから読む）。</summary>
public static class OggReader
{
    public static TagSet Read(string path)
    {
        var tags = new TagSet();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var head = new byte[Math.Min(stream.Length, 256 * 1024)];
        stream.ReadExactly(head);
        // 「\x03vorbis」の後にコメントヘッダ（Opus なら「OpusTags」）
        int start = IndexOf(head, "\x03vorbis"u8) is var v && v >= 0 ? v + 7 : IndexOf(head, "OpusTags"u8) is var o && o >= 0 ? o + 8 : -1;
        if (start < 0 || start + 4 > head.Length)
        {
            return tags;
        }
        var comments = ReadComments(head, start);
        foreach (var (key, value) in comments)
        {
            tags.SetIfMissing("OGG." + key, value);
        }
        tags.SetIfMissing("ID3.Artist", tags["OGG.ARTIST"]);
        tags.SetIfMissing("ID3.Album", tags["OGG.ALBUM"]);
        tags.SetIfMissing("ID3.Track", tags["OGG.TITLE"]);
        tags.SetIfMissing("ID3.Year", tags["OGG.DATE"]);
        tags.SetIfMissing("ID3.Comment", tags["OGG.COMMENT"].IsEmpty ? tags["OGG.DESCRIPTION"] : tags["OGG.COMMENT"]);
        tags.SetIfMissing("ID3.Genre", tags["OGG.GENRE"]);
        tags.SetIfMissing("ID3.TrackNumber", Id3Reader.NormalizeTrackNumber(tags["OGG.TRACKNUMBER"].Text));
        AudioTags.CopyToId3v1(tags);
        return tags;
    }

    /// <summary>Vorbis コメントの並び（ベンダー文字列の後に件数と「KEY=値」）。Ogg のページ境界をまたぐ場合は読めない範囲で打ち切る。</summary>
    private static List<(string, string)> ReadComments(byte[] b, int pos)
    {
        var list = new List<(string, string)>();
        int vendorLength = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(pos));
        pos += 4 + vendorLength;
        if (pos + 4 > b.Length)
        {
            return list;
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(pos));
        pos += 4;
        for (int i = 0; i < count && pos + 4 <= b.Length; i++)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(pos));
            pos += 4;
            if (length < 0 || pos + length > b.Length)
            {
                break;
            }
            string entry = Encoding.UTF8.GetString(b, pos, length);
            pos += length;
            int eq = entry.IndexOf('=');
            if (eq > 0)
            {
                list.Add((entry[..eq].ToUpperInvariant(), entry[(eq + 1)..]));
            }
        }
        return list;
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle) => haystack.AsSpan().IndexOf(needle);
}
