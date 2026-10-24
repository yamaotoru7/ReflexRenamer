using System.Text;

namespace FlexibleRenamer.Core.Ini;

/// <summary>
/// オリジナル版の INI ファイル（BOM 付き UTF-16 LE）を読み込む。
/// 同じキーが繰り返し現れる形式（<c>Preset=</c> の並びなど）に対応するため、
/// セクション内のエントリは出現順のリストで保持する。
/// </summary>
public sealed class IniDocument
{
    private readonly List<IniSection> _sections = [];

    public IReadOnlyList<IniSection> Sections => _sections;

    /// <summary>
    /// 名前が一致する最初のセクションを返す（大文字小文字は区別しない）。
    /// </summary>
    public IniSection? this[string name] =>
        _sections.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    public static IniDocument Load(string path)
    {
        // BOM があればそのエンコーディングで読み、なければ UTF-16 LE とみなす
        using var reader = new StreamReader(path, Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
        return Parse(reader);
    }

    public static IniDocument Parse(TextReader reader)
    {
        var document = new IniDocument();
        IniSection? current = null;

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == ';')
            {
                continue;
            }

            if (line[0] == '[')
            {
                int close = line.IndexOf(']');
                if (close > 0)
                {
                    current = new IniSection(line[1..close]);
                    document._sections.Add(current);
                    continue;
                }
            }

            int eq = line.IndexOf('=');
            if (current is null || eq < 0)
            {
                continue;
            }

            current.Add(line[..eq], line[(eq + 1)..]);
        }

        return document;
    }

    /// <summary>セクションを返す。なければ末尾に作る。</summary>
    public IniSection GetOrAdd(string name)
    {
        var section = this[name];
        if (section is null)
        {
            section = new IniSection(name);
            _sections.Add(section);
        }
        return section;
    }

    /// <summary>オリジナル版と同じ形式（BOM 付き UTF-16 LE、CRLF、セクションの間に空行）で保存する。</summary>
    public void Save(string path)
    {
        using var writer = new StreamWriter(path, append: false, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));
        writer.NewLine = "\r\n";
        bool first = true;
        foreach (var section in _sections)
        {
            if (!first)
            {
                writer.WriteLine();
            }
            first = false;
            writer.WriteLine($"[{section.Name}]");
            foreach (var (key, value) in section.Entries)
            {
                writer.WriteLine($"{key}={value}");
            }
        }
    }
}

public sealed class IniSection(string name)
{
    private readonly List<KeyValuePair<string, string>> _entries = [];

    public string Name { get; } = name;

    public IReadOnlyList<KeyValuePair<string, string>> Entries => _entries;

    /// <summary>キーの最初の値を置き換える。なければ末尾に足す。</summary>
    public void Set(string key, string value)
    {
        int i = _entries.FindIndex(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
        if (i < 0)
        {
            _entries.Add(new(key, value));
        }
        else
        {
            _entries[i] = new(key, value);
        }
    }

    /// <summary>同じキーの値をすべて入れ替える（MRU の一覧など）。位置は最初の出現箇所。</summary>
    public void SetAll(string key, IEnumerable<string> values)
    {
        int i = _entries.FindIndex(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
        _entries.RemoveAll(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
        var items = values.Select(v => new KeyValuePair<string, string>(key, v)).ToList();
        _entries.InsertRange(i < 0 ? _entries.Count : i, items);
    }

    /// <summary>
    /// キーが一致する最初の値を返す（大文字小文字は区別しない）。
    /// </summary>
    public string? this[string key] =>
        _entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>
    /// キーが一致するすべての値を出現順に返す。
    /// </summary>
    public IEnumerable<string> GetAll(string key) =>
        _entries.Where(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase)).Select(e => e.Value);

    internal void Add(string key, string value) => _entries.Add(new(key, value));
}
