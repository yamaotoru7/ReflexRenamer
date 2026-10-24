using System.Text;
using System.Text.RegularExpressions;
using ReflexRenamer.Core.Ini;

namespace ReflexRenamer.Core.Localization;

/// <summary>
/// 言語ファイル（<c>Flexible Renamer.&lt;言語&gt;.Strings.ini</c>）。
/// キーは英語の原文で、<c>[Resource]</c> に UI 文字列、<c>[Preset]</c> にプリセット名と定型メニュー名の訳がある。
/// 値の中の <c>%%Key%%</c> は、そのキーの訳に置き換える。
/// </summary>
public sealed partial class LanguageStrings
{
    private readonly Dictionary<string, string> _resource;
    private readonly Dictionary<string, string> _preset;
    private readonly IniDocument _ini;

    private LanguageStrings(string code, IniDocument ini)
    {
        Code = code;
        var language = ini["Language"];
        ShortName = language?["ShortName"] ?? "";
        Name = language?["Name"] ?? "";
        Translator = language?["Translator"] ?? "";
        Version = language?["Version"] ?? "";
        _resource = ToMap(ini["Resource"]);
        _preset = ToMap(ini["Preset"]);
        _ini = ini;
    }

    /// <summary>ロケール名（<c>[Language]=ja-JP</c> の値）。</summary>
    public string Code { get; }

    public string ShortName { get; }

    public string Name { get; }

    public string Translator { get; }

    public string Version { get; }

    public static LanguageStrings Load(string path)
    {
        var ini = IniDocument.Load(path);
        // 先頭行は「[Language]=ja-JP」という形で、セクション名の後ろにロケール名がある
        string code = "";
        using (var reader = new StreamReader(path, Encoding.Unicode, detectEncodingFromByteOrderMarks: true))
        {
            string? first = reader.ReadLine();
            if (first is not null && first.StartsWith("[Language]=", StringComparison.Ordinal))
            {
                code = first["[Language]=".Length..];
            }
        }
        return new LanguageStrings(code, ini);
    }

    /// <summary>UI 文字列を訳す。訳がなければ原文のまま。<c>%%Key%%</c> も展開する。</summary>
    public string Translate(string key) =>
        // テンプレートの文字列には末尾に空白が付いたものがある（DIALOG 172 の説明文）。訳語のキーは Trim されているので、そちらでも引く
        Expand(_resource.TryGetValue(key, out var v) || _resource.TryGetValue(key.Trim(), out v) ? v : key);

    /// <summary>プリセット名・定型メニュー名を訳す。訳がなければ原文のまま。</summary>
    public string TranslatePreset(string name) => Expand(_preset.TryGetValue(name, out var v) ? v : name);

    /// <summary>
    /// 任意のセクションの値（タグの表示名 <c>[MusicTag] &lt;ID3.Artist&gt;</c>、EXIF の列挙値 <c>&lt;EXIF.Flash&gt;(0001)</c> など）。
    /// <c>%%Key%%</c> を展開して返す。なければ null。
    /// </summary>
    public string? Lookup(string section, string key)
    {
        var sec = _ini[section];
        if (sec is null)
        {
            return null;
        }
        foreach (var (k, v) in sec.Entries)
        {
            if (string.Equals(k.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return Expand(Unescape(v.Trim()));
            }
        }
        return null;
    }

    /// <summary>文字列中の <c>%%Key%%</c> を訳に置き換える。</summary>
    public string Expand(string text)
    {
        for (int depth = 0; depth < 8 && text.Contains("%%", StringComparison.Ordinal); depth++)
        {
            string next = Token().Replace(text, m => _resource.TryGetValue(m.Groups[1].Value.Trim(), out var v) ? v : m.Groups[1].Value);
            if (next == text)
            {
                break;
            }
            text = next;
        }
        return text;
    }

    private static Dictionary<string, string> ToMap(IniSection? section)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (section is null)
        {
            return map;
        }
        foreach (var (key, value) in section.Entries)
        {
            if (key.StartsWith(';'))
            {
                continue;
            }
            // 同じキーが複数回出てくる（「Copy」など）ときは最初のものを使う。
            // オリジナル版は GetPrivateProfileString で読むので、キーと値の前後の空白は削られる
            // （「Shortcut to = へのショートカット」は「Shortcut to」→「へのショートカット」になる）
            map.TryAdd(Unescape(key.Trim()), Unescape(value.Trim()));
        }
        return map;
    }

    /// <summary><c>\\</c>、<c>\n</c>、<c>\t</c> のエスケープを戻す。</summary>
    public static string Unescape(string s)
    {
        if (!s.Contains('\\'))
        {
            return s;
        }
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[i + 1];
                switch (n)
                {
                    case '\\':
                        sb.Append('\\');
                        i++;
                        continue;
                    case 'n':
                        sb.Append('\n');
                        i++;
                        continue;
                    case 't':
                        sb.Append('\t');
                        i++;
                        continue;
                }
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    [GeneratedRegex("%%(.*?)%%")]
    private static partial Regex Token();
}
