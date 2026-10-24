using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace FlexibleRenamer.Core.Tags;

/// <summary>
/// EXIF（JPEG の APP1、または TIFF）。列挙値（フラッシュなど）は4桁の16進の文字列にし、表示時に言語ファイルの対応表で名前にする。
/// </summary>
public static class ExifReader
{
    private static readonly Dictionary<int, string> TagNames = new()
    {
        [0x010E] = "ImageDescription",
        [0x010F] = "Maker",
        [0x0110] = "Model",
        [0x0131] = "Software",
        [0x013B] = "Artist",
        [0x8298] = "Copyright",
        [0x9003] = "OriginalTime",
        [0x9286] = "UserComment",
        [0xA004] = "RelatedSoundFile",
        [0xA001] = "ColorSpace",
        [0x9209] = "Flash",
        [0x920A] = "FocalLength",
        [0x829D] = "F-Number",
        [0x829A] = "ExposureTime",
        [0x8827] = "ISOSpeed",
        [0x9207] = "MeteringMode",
        [0x9208] = "LightSource",
        [0x8822] = "ExposureProgram",
        [0x9204] = "ExposureCompensation",
    };

    /// <summary>列挙値として16進4桁にするタグ。</summary>
    private static readonly HashSet<int> EnumTags = [0xA001, 0x9209, 0x9207, 0x9208, 0x8822];

    public static TagSet Read(string path)
    {
        var tags = new TagSet();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var head = new byte[2];
        if (stream.Read(head, 0, 2) < 2)
        {
            return tags;
        }
        if (head[0] == 0xFF && head[1] == 0xD8)
        {
            var app1 = JpegSegments.Find(stream, 0xE1, "Exif\0\0"u8);
            if (app1 is not null)
            {
                ReadTiff(app1, tags);
            }
        }
        else if ((head[0] == 'I' && head[1] == 'I') || (head[0] == 'M' && head[1] == 'M'))
        {
            stream.Position = 0;
            var tiff = new byte[Math.Min(stream.Length, 4 * 1024 * 1024)];
            stream.ReadExactly(tiff);
            ReadTiff(tiff, tags);
        }
        return tags;
    }

    private static void ReadTiff(byte[] t, TagSet tags)
    {
        if (t.Length < 8)
        {
            return;
        }
        bool little = t[0] == 'I';
        uint ifd = U32(t, 4, little);
        var visited = new HashSet<uint>();
        ReadIfd(t, ifd, little, tags, visited);
    }

    private static void ReadIfd(byte[] t, uint offset, bool little, TagSet tags, HashSet<uint> visited)
    {
        if (offset + 2 > t.Length || !visited.Add(offset))
        {
            return;
        }
        int count = U16(t, (int)offset, little);
        for (int i = 0; i < count; i++)
        {
            int e = (int)offset + 2 + i * 12;
            if (e + 12 > t.Length)
            {
                return;
            }
            int tag = U16(t, e, little);
            int type = U16(t, e + 2, little);
            uint n = U32(t, e + 4, little);
            int size = TypeSize(type) * (int)Math.Min(n, 1 << 20);
            int valueOffset = size <= 4 ? e + 8 : (int)U32(t, e + 8, little);
            if (valueOffset < 0 || valueOffset + size > t.Length)
            {
                continue;
            }
            if (tag == 0x8769)
            {
                ReadIfd(t, U32(t, e + 8, little), little, tags, visited);
                continue;
            }
            if (!TagNames.TryGetValue(tag, out var name))
            {
                continue;
            }
            var value = Format(t, valueOffset, type, (int)n, little, tag);
            if (value is { } v)
            {
                tags.SetIfMissing("EXIF." + name, v);
            }
        }
    }

    private static TagValue? Format(byte[] t, int offset, int type, int count, bool little, int tag)
    {
        switch (type)
        {
            case 2: // ASCII
            {
                string s = Encoding.UTF8.GetString(t, offset, count).TrimEnd('\0').Trim();
                if (tag == 0x9003 && DateTime.TryParseExact(s, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    return time;
                }
                return s;
            }
            case 7: // UNDEFINED（ユーザーコメントは先頭8バイトが文字コード）
            {
                if (tag != 0x9286 || count < 8)
                {
                    return null;
                }
                string code = Encoding.ASCII.GetString(t, offset, 8).TrimEnd('\0');
                var data = new ReadOnlySpan<byte>(t, offset + 8, count - 8);
                return (code switch
                {
                    "UNICODE" => (little ? Encoding.Unicode : Encoding.BigEndianUnicode).GetString(data),
                    "JIS" => Encoding.UTF8.GetString(data),
                    _ => Encoding.UTF8.GetString(data),
                }).TrimEnd('\0').Trim();
            }
            case 3: // SHORT
            {
                int v = U16(t, offset, little);
                return EnumTags.Contains(tag) ? v.ToString("x4", CultureInfo.InvariantCulture) : v.ToString(CultureInfo.InvariantCulture);
            }
            case 4:
                return U32(t, offset, little).ToString(CultureInfo.InvariantCulture);
            case 5: // RATIONAL
            case 10: // SRATIONAL
            {
                long num = type == 5 ? U32(t, offset, little) : (int)U32(t, offset, little);
                long den = type == 5 ? U32(t, offset + 4, little) : (int)U32(t, offset + 4, little);
                if (den == 0)
                {
                    return null;
                }
                double d = (double)num / den;
                return tag switch
                {
                    0x829A => d.ToString("0.####", CultureInfo.InvariantCulture), // 露出時間：表示側で 1/n 秒にする
                    0x920A => d.ToString("0.#", CultureInfo.InvariantCulture) + " mm",
                    0x829D => "F" + d.ToString("0.#", CultureInfo.InvariantCulture),
                    _ => d.ToString("0.##", CultureInfo.InvariantCulture),
                };
            }
            default:
                return null;
        }
    }

    private static int TypeSize(int type) => type switch
    {
        1 or 2 or 6 or 7 => 1,
        3 or 8 => 2,
        4 or 9 or 11 => 4,
        5 or 10 or 12 => 8,
        _ => 1,
    };

    private static int U16(byte[] b, int i, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(i)) : BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(i));

    private static uint U32(byte[] b, int i, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(i)) : BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(i));
}

/// <summary>IPTC-IIM（JPEG の APP13「Photoshop 3.0」、または PSD の画像リソース）。</summary>
public static class IptcReader
{
    private static readonly Dictionary<int, string> DataSets = new()
    {
        [5] = "ObjectName",
        [15] = "Category",
        [55] = "DateCreated",
        [80] = "Byline",
        [85] = "BylineTitle",
        [90] = "City",
        [95] = "State",
        [101] = "Country",
        [103] = "OriginalTransmissionReference",
        [105] = "Headline",
        [110] = "Credits",
        [116] = "CopyrightNotice",
        [120] = "Caption",
        [122] = "CaptionWriter",
    };

    public static TagSet Read(string path)
    {
        var tags = new TagSet();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var head = new byte[2];
        if (stream.Read(head, 0, 2) < 2 || head[0] != 0xFF || head[1] != 0xD8)
        {
            return tags;
        }
        var app13 = JpegSegments.Find(stream, 0xED, "Photoshop 3.0\0"u8);
        if (app13 is not null)
        {
            ReadImageResources(app13, tags);
        }
        return tags;
    }

    public static TagSet ReadPsd(string path)
    {
        var tags = new TagSet();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = new byte[26];
        if (stream.Read(header, 0, 26) < 26 || Encoding.ASCII.GetString(header, 0, 4) != "8BPS")
        {
            return tags;
        }
        var lengthBytes = new byte[4];
        stream.ReadExactly(lengthBytes);
        stream.Position += BinaryPrimitives.ReadUInt32BigEndian(lengthBytes); // カラーモードデータ
        stream.ReadExactly(lengthBytes);
        uint resourcesLength = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        var resources = new byte[Math.Min(resourcesLength, 16 * 1024 * 1024)];
        stream.ReadExactly(resources);
        ReadImageResources(resources, tags);
        return tags;
    }

    /// <summary>8BIM リソースの並びから IPTC（ID 0x0404）を探す。</summary>
    private static void ReadImageResources(byte[] b, TagSet tags)
    {
        int pos = 0;
        while (pos + 12 <= b.Length)
        {
            if (!(b[pos] == '8' && b[pos + 1] == 'B' && b[pos + 2] == 'I' && b[pos + 3] == 'M'))
            {
                break;
            }
            int id = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos + 4));
            int nameLength = b[pos + 6];
            int nameEnd = pos + 7 + nameLength;
            if ((nameLength + 1) % 2 == 1)
            {
                nameEnd++;
            }
            int size = (int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(nameEnd));
            int data = nameEnd + 4;
            if (data + size > b.Length)
            {
                break;
            }
            if (id == 0x0404)
            {
                ReadIim(b.AsSpan(data, size), tags);
                return;
            }
            pos = data + size + (size % 2);
        }
    }

    private static void ReadIim(ReadOnlySpan<byte> d, TagSet tags)
    {
        var text = Encoding.UTF8;
        int pos = 0;
        while (pos + 5 <= d.Length)
        {
            if (d[pos] != 0x1C)
            {
                pos++;
                continue;
            }
            int record = d[pos + 1];
            int dataSet = d[pos + 2];
            int length = BinaryPrimitives.ReadUInt16BigEndian(d[(pos + 3)..]);
            pos += 5;
            if (pos + length > d.Length)
            {
                break;
            }
            if (record == 1 && dataSet == 90 && length >= 3 && d[pos] == 0x1B && d[pos + 1] == 0x25 && d[pos + 2] == 0x47)
            {
                text = Encoding.UTF8; // 文字コードの指定（UTF-8）
            }
            else if (record == 2 && DataSets.TryGetValue(dataSet, out var name))
            {
                // DateCreated（2:55）も日時にせず文字列のまま（オリジナル版の実測：golden tagrename-formats の f-image-date は「20240315」）
                tags.SetIfMissing("IPTC." + name, text.GetString(d.Slice(pos, length)).Trim());
            }
            pos += length;
        }
    }
}

/// <summary>JPEG のセグメント走査。</summary>
internal static class JpegSegments
{
    /// <summary>指定のマーカーで、データの先頭が signature で始まるセグメントの中身（signature の後ろ）を返す。</summary>
    public static byte[]? Find(Stream stream, int marker, ReadOnlySpan<byte> signature)
    {
        stream.Position = 2;
        var header = new byte[4];
        while (stream.Read(header, 0, 4) == 4)
        {
            if (header[0] != 0xFF)
            {
                return null;
            }
            int m = header[1];
            int length = (header[2] << 8) | header[3];
            if (m == 0xDA || m == 0xD9 || length < 2)
            {
                return null;
            }
            if (m == marker)
            {
                var data = new byte[length - 2];
                stream.ReadExactly(data);
                if (data.AsSpan().StartsWith(signature))
                {
                    return data[signature.Length..];
                }
            }
            else
            {
                stream.Position += length - 2;
            }
        }
        return null;
    }
}
