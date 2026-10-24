namespace FlexibleRenamer.Core.Tags;

/// <summary>タグの値。日時はそのまま持ち、書式は表示時に決める。</summary>
public readonly record struct TagValue(string? Text, DateTime? Time = null)
{
    public static implicit operator TagValue(string? text) => new(text);

    public static implicit operator TagValue(DateTime time) => new(null, time);

    public bool IsEmpty => Text is null && Time is null;
}

/// <summary>ファイルから読んだタグ。キーは <c>ID3.Artist</c> のような名前（大文字小文字を区別しない）。</summary>
public sealed class TagSet
{
    private readonly Dictionary<string, TagValue> _values = new(StringComparer.OrdinalIgnoreCase);

    public static readonly TagSet Empty = new();

    public TagValue this[string name] => _values.TryGetValue(name, out var v) ? v : default;

    public IReadOnlyDictionary<string, TagValue> Values => _values;

    public void Set(string name, TagValue value)
    {
        if (!value.IsEmpty && !(value.Text is { Length: 0 }))
        {
            _values[name] = value;
        }
    }

    /// <summary>まだないときだけ入れる（ID3v2 を v1 より優先するときなど）。</summary>
    public void SetIfMissing(string name, TagValue value)
    {
        if (!_values.ContainsKey(name))
        {
            Set(name, value);
        }
    }
}

/// <summary>
/// 拡張子からタグの読み取り方を選ぶ。読めないファイル・壊れたファイルは空のタグとして扱う。
/// </summary>
public static class TagReader
{
    public static TagSet Read(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            return ext switch
            {
                "mp3" => Id3Reader.Read(path),
                "wma" or "asf" or "wmv" => AsfReader.Read(path),
                "ape" or "mpc" or "mp+" => ApeReader.Read(path),
                "ogg" or "oga" => OggReader.Read(path),
                "jpg" or "jpe" or "jpeg" or "tif" or "tiff" => ImageTags(path),
                "psd" => IptcReader.ReadPsd(path),
                "doc" or "xls" or "ppt" or "vsd" or "mpp" => OleDocReader.Read(path),
                "docx" or "xlsx" or "pptx" => OoxmlDocReader.Read(path),
                "pdf" => PdfReader.Read(path),
                "htm" or "html" or "shtml" or "xhtml" => HtmlReader.Read(path),
                _ => TagSet.Empty,
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or IndexOutOfRangeException or ArgumentOutOfRangeException or OverflowException)
        {
            return TagSet.Empty;
        }
    }

    private static TagSet ImageTags(string path)
    {
        var tags = ExifReader.Read(path);
        foreach (var (k, v) in IptcReader.Read(path).Values)
        {
            tags.Set(k, v);
        }
        return tags;
    }
}
