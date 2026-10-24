using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FlexibleRenamer.Core.Tags;

/// <summary>
/// Office 97-2003 などの OLE 複合文書の文書情報（SummaryInformation / DocumentSummaryInformation）。
/// </summary>
public static class OleDocReader
{
    private static readonly byte[] Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static TagSet Read(string path)
    {
        var tags = new TagSet();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var file = new CompoundFile(stream);
        if (!file.IsValid)
        {
            return tags;
        }
        if (file.ReadStream("\u0005SummaryInformation") is { } summary)
        {
            var props = PropertySet.Read(summary);
            tags.Set("DOC.Title", props.Text(2));
            tags.Set("DOC.Subject", props.Text(3));
            tags.Set("DOC.Author", props.Text(4));
            tags.Set("DOC.Keywords", props.Text(5));
            tags.Set("DOC.Comments", props.Text(6));
            if (props.Time(12) is { } created) tags.Set("DOC.Created", created);
            if (props.Time(13) is { } saved) tags.Set("DOC.LastSaved", saved);
            tags.Set("DOC.AppName", props.Text(18));
        }
        // DOC.Company（DocumentSummaryInformation の PID 15）はオリジナル版では OLE 複合文書から読まれず、常に空になる
        // （golden tagrename-docs：ANSI・Unicode どちらのプロパティセットでも空）。そのため読まない
        return tags;
    }

    /// <summary>複合文書の最小限の読み取り（ルート直下のストリームだけ）。</summary>
    private sealed class CompoundFile
    {
        private readonly byte[] _data;
        private readonly int _sectorSize;
        private readonly int _miniSectorSize;
        private readonly int[] _fat;
        private readonly int[] _miniFat;
        private readonly byte[] _directory;
        private readonly byte[] _miniStream;
        private readonly uint _miniCutoff;

        public bool IsValid { get; }

        public CompoundFile(Stream stream)
        {
            _data = new byte[Math.Min(stream.Length, 64L * 1024 * 1024)];
            stream.ReadExactly(_data);
            _fat = [];
            _miniFat = [];
            _directory = [];
            _miniStream = [];
            if (_data.Length < 512 || !_data.AsSpan(0, 8).SequenceEqual(Signature))
            {
                return;
            }
            _sectorSize = 1 << BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(30));
            _miniSectorSize = 1 << BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(32));
            int fatSectors = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(44));
            int firstDirectory = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(48));
            _miniCutoff = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(56));
            int firstMiniFat = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(60));
            int miniFatSectors = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(64));
            int firstDifat = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(68));

            // DIFAT（ヘッダに 109 個、残りは DIFAT セクタ）から FAT セクタの位置を集める
            var fatSectorIds = new List<int>();
            for (int i = 0; i < 109 && fatSectorIds.Count < fatSectors; i++)
            {
                fatSectorIds.Add(BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(76 + i * 4)));
            }
            int difat = firstDifat;
            int perSector = _sectorSize / 4 - 1;
            while (difat >= 0 && fatSectorIds.Count < fatSectors && SectorOffset(difat) + _sectorSize <= _data.Length)
            {
                int off = SectorOffset(difat);
                for (int i = 0; i < perSector && fatSectorIds.Count < fatSectors; i++)
                {
                    fatSectorIds.Add(BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(off + i * 4)));
                }
                difat = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(off + perSector * 4));
            }
            _fat = ReadInts(fatSectorIds);
            _directory = ReadChain(firstDirectory, _fat, _sectorSize, SectorOffset, int.MaxValue);

            var miniFatIds = new List<int>();
            for (int s = firstMiniFat, n = 0; s >= 0 && n < miniFatSectors; s = Next(_fat, s), n++)
            {
                miniFatIds.Add(s);
            }
            _miniFat = ReadInts(miniFatIds);
            // ミニストリームはルートエントリの開始セクタから
            int rootStart = BinaryPrimitives.ReadInt32LittleEndian(_directory.AsSpan(116));
            int rootSize = BinaryPrimitives.ReadInt32LittleEndian(_directory.AsSpan(120));
            _miniStream = _directory.Length >= 128 ? ReadChain(rootStart, _fat, _sectorSize, SectorOffset, rootSize) : [];
            IsValid = true;
        }

        public byte[]? ReadStream(string name)
        {
            for (int e = 0; e + 128 <= _directory.Length; e += 128)
            {
                int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(_directory.AsSpan(e + 64));
                if (nameLength < 2 || _directory[e + 66] != 2)
                {
                    continue;
                }
                string entryName = Encoding.Unicode.GetString(_directory, e, nameLength - 2);
                if (entryName != name)
                {
                    continue;
                }
                int start = BinaryPrimitives.ReadInt32LittleEndian(_directory.AsSpan(e + 116));
                int size = BinaryPrimitives.ReadInt32LittleEndian(_directory.AsSpan(e + 120));
                return (uint)size < _miniCutoff
                    ? ReadChain(start, _miniFat, _miniSectorSize, s => s * _miniSectorSize, size, _miniStream)
                    : ReadChain(start, _fat, _sectorSize, SectorOffset, size);
            }
            return null;
        }

        private int SectorOffset(int sector) => (sector + 1) * _sectorSize;

        private static int Next(int[] fat, int sector) => sector >= 0 && sector < fat.Length ? fat[sector] : -1;

        private int[] ReadInts(List<int> sectors)
        {
            var result = new List<int>();
            foreach (int s in sectors)
            {
                int off = SectorOffset(s);
                if (s < 0 || off + _sectorSize > _data.Length)
                {
                    break;
                }
                for (int i = 0; i < _sectorSize / 4; i++)
                {
                    result.Add(BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(off + i * 4)));
                }
            }
            return result.ToArray();
        }

        private byte[] ReadChain(int start, int[] fat, int sectorSize, Func<int, int> offsetOf, int size, byte[]? source = null)
        {
            source ??= _data;
            using var ms = new MemoryStream();
            var seen = new HashSet<int>();
            for (int s = start; s >= 0 && seen.Add(s) && ms.Length < size; s = Next(fat, s))
            {
                int off = offsetOf(s);
                if (off < 0 || off + sectorSize > source.Length)
                {
                    break;
                }
                ms.Write(source, off, (int)Math.Min(sectorSize, size - ms.Length));
            }
            return ms.ToArray();
        }
    }

    /// <summary>プロパティセットストリーム（最初のセクションだけ）。</summary>
    private sealed class PropertySet
    {
        private readonly Dictionary<int, object> _values = [];

        public string? Text(int id) => _values.TryGetValue(id, out var v) ? v as string : null;

        public DateTime? Time(int id) => _values.TryGetValue(id, out var v) && v is DateTime t ? t : null;

        public static PropertySet Read(byte[] b)
        {
            var set = new PropertySet();
            if (b.Length < 48)
            {
                return set;
            }
            int codePage = 1252;
            int sectionOffset = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(44));
            if (sectionOffset + 8 > b.Length)
            {
                return set;
            }
            int count = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(sectionOffset + 4));
            var entries = new List<(int Id, int Offset)>();
            for (int i = 0; i < count && sectionOffset + 8 + i * 8 + 8 <= b.Length; i++)
            {
                entries.Add((BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(sectionOffset + 8 + i * 8)), BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(sectionOffset + 12 + i * 8))));
            }
            foreach (var (id, off) in entries)
            {
                if (id == 1 && sectionOffset + off + 8 <= b.Length)
                {
                    codePage = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(sectionOffset + off + 4));
                }
            }
            Encoding encoding;
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                encoding = codePage == 1200 ? Encoding.Unicode : Encoding.GetEncoding(codePage);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                encoding = Encoding.Latin1;
            }
            foreach (var (id, off) in entries)
            {
                int p = sectionOffset + off;
                if (p + 8 > b.Length)
                {
                    continue;
                }
                int type = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));
                switch (type)
                {
                    case 30: // VT_LPSTR
                    case 31: // VT_LPWSTR
                    {
                        int length = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p + 4));
                        int bytes = type == 31 ? length * 2 : length;
                        if (length < 0 || p + 8 + bytes > b.Length)
                        {
                            break;
                        }
                        string s = (type == 31 ? Encoding.Unicode : encoding).GetString(b, p + 8, bytes).TrimEnd('\0');
                        set._values[id] = s;
                        break;
                    }
                    case 64: // VT_FILETIME
                    {
                        long ft = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(p + 4));
                        if (ft > 0)
                        {
                            set._values[id] = DateTime.FromFileTime(ft);
                        }
                        break;
                    }
                }
            }
            return set;
        }
    }
}

/// <summary>Office 2007 以降（docx/xlsx/pptx）の docProps/core.xml と app.xml。</summary>
public static class OoxmlDocReader
{
    private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Dcterms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

    public static TagSet Read(string path)
    {
        var tags = new TagSet();
        using var zip = ZipFile.OpenRead(path);
        if (zip.GetEntry("docProps/core.xml") is { } core)
        {
            using var s = core.Open();
            var root = XDocument.Load(s).Root!;
            tags.Set("DOC.Title", root.Element(Dc + "title")?.Value);
            tags.Set("DOC.Subject", root.Element(Dc + "subject")?.Value);
            tags.Set("DOC.Author", root.Element(Dc + "creator")?.Value);
            tags.Set("DOC.Keywords", root.Element(Cp + "keywords")?.Value);
            tags.Set("DOC.Comments", root.Element(Dc + "description")?.Value);
            if (DateTime.TryParse(root.Element(Dcterms + "created")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var created))
            {
                tags.Set("DOC.Created", created.ToLocalTime());
            }
            if (DateTime.TryParse(root.Element(Dcterms + "modified")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var modified))
            {
                tags.Set("DOC.LastSaved", modified.ToLocalTime());
            }
        }
        if (zip.GetEntry("docProps/app.xml") is { } app)
        {
            using var s = app.Open();
            var root = XDocument.Load(s).Root!;
            tags.Set("DOC.AppName", root.Element(Ep + "Application")?.Value);
            tags.Set("DOC.Company", root.Element(Ep + "Company")?.Value);
        }
        return tags;
    }
}

/// <summary>PDF の文書情報辞書（暗号化や圧縮オブジェクトストリームの中にあるものは読めない）。</summary>
public static partial class PdfReader
{
    public static TagSet Read(string path)
    {
        var tags = new TagSet();
        var bytes = File.ReadAllBytes(path);
        string text = Encoding.Latin1.GetString(bytes);
        foreach (var (key, name) in new[] { ("Title", "DOC.Title"), ("Subject", "DOC.Subject"), ("Author", "DOC.Author"), ("Keywords", "DOC.Keywords"), ("Creator", "DOC.AppName") })
        {
            if (FindString(text, key) is { } value)
            {
                tags.Set(name, value);
            }
        }
        if (FindString(text, "CreationDate") is { } created && ParseDate(created) is { } c) tags.Set("DOC.Created", c);
        if (FindString(text, "ModDate") is { } modified && ParseDate(modified) is { } m) tags.Set("DOC.LastSaved", m);
        return tags;
    }

    /// <summary>「/Key (文字列)」または「/Key &lt;16進&gt;」。最後に出てくるもの（更新された情報）を使う。</summary>
    private static string? FindString(string text, string key)
    {
        string? result = null;
        foreach (Match m in StringValue().Matches(text))
        {
            if (m.Groups["key"].Value != key)
            {
                continue;
            }
            result = m.Groups["hex"].Success ? DecodeHex(m.Groups["hex"].Value) : DecodeLiteral(m.Groups["lit"].Value);
        }
        return result;
    }

    [GeneratedRegex(@"/(?<key>[A-Za-z]+)\s*(?:\((?<lit>(?:\\.|[^\\)])*)\)|<(?<hex>[0-9A-Fa-f\s]*)>)")]
    private static partial Regex StringValue();

    private static string DecodeLiteral(string s)
    {
        var bytes = new List<byte>();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                switch (n)
                {
                    case 'n': bytes.Add((byte)'\n'); break;
                    case 'r': bytes.Add((byte)'\r'); break;
                    case 't': bytes.Add((byte)'\t'); break;
                    case >= '0' and <= '7':
                    {
                        int code = n - '0';
                        for (int k = 0; k < 2 && i + 1 < s.Length && s[i + 1] is >= '0' and <= '7'; k++)
                        {
                            code = code * 8 + (s[++i] - '0');
                        }
                        bytes.Add((byte)code);
                        break;
                    }
                    default: bytes.Add((byte)n); break;
                }
            }
            else
            {
                bytes.Add((byte)c);
            }
        }
        return DecodeBytes(bytes.ToArray());
    }

    private static string DecodeHex(string hex)
    {
        hex = Regex.Replace(hex, @"\s", "");
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return DecodeBytes(bytes);
    }

    /// <summary>BOM があれば UTF-16BE、なければ PDFDocEncoding（Latin-1 とみなす）。</summary>
    private static string DecodeBytes(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF
            ? Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2)
            : Encoding.Latin1.GetString(bytes);

    /// <summary>「D:YYYYMMDDHHmmSS」の日付。</summary>
    private static DateTime? ParseDate(string s)
    {
        string digits = new(s.StartsWith("D:", StringComparison.Ordinal) ? s[2..].TakeWhile(char.IsDigit).ToArray() : s.TakeWhile(char.IsDigit).ToArray());
        return digits.Length >= 8 && DateTime.TryParseExact(digits.PadRight(14, '0'), "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;
    }
}

/// <summary>HTML の &lt;title&gt;。</summary>
public static partial class HtmlReader
{
    public static TagSet Read(string path)
    {
        var tags = new TagSet();
        var bytes = File.ReadAllBytes(path);
        string text = DetectEncoding(bytes).GetString(bytes);
        var m = Title().Match(text);
        if (m.Success)
        {
            tags.Set("HTML.Title", System.Net.WebUtility.HtmlDecode(Regex.Replace(m.Groups[1].Value, @"\s+", " ")).Trim());
        }
        return tags;
    }

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Title();

    [GeneratedRegex(@"charset\s*=\s*[""']?\s*([A-Za-z0-9_\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Charset();

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8;
        }
        var m = Charset().Match(Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4096)));
        if (m.Success)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(m.Groups[1].Value);
            }
            catch (ArgumentException)
            {
            }
        }
        return Encoding.UTF8;
    }
}
