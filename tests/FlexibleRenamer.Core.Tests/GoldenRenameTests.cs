using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlexibleRenamer.Core.Localization;
using FlexibleRenamer.Core.Presets;
using FlexibleRenamer.Core.Rename;
using FlexibleRenamer.Core.Scripting;
using FlexibleRenamer.Core.Tags;

namespace FlexibleRenamer.Core.Tests;

/// <summary>
/// オリジナル版のプレビュー一覧から取った正解データ（Golden/*.json、`FrTools golden` で生成）と照合する。
/// フィクスチャを一時フォルダに実際に作り、ファイル一覧の収集からプレビューまでを通して比べる。
/// </summary>
/// <remarks>
/// 正解データは日本語の Windows（ユーザー設定の短い日付形式を含む）で取ったので、
/// 日付の書式やタイムゾーン名が絡むケースは同じ環境でしか一致しない。
/// </remarks>
public class GoldenRenameTests
{
    private static readonly string GoldenDir = Path.Combine(AppContext.BaseDirectory, "Golden");

    /// <summary>ランダムな数字（<c>*</c>）を含むケース。数字を伏せて比べる。</summary>
    private static readonly HashSet<string> RandomCases = ["rp-star-random", "rp-escaped-q", "tr-single-dest"];

    private static readonly ConcurrentDictionary<string, string> Materialized = new();

    /// <summary>
    /// オリジナル版の挙動を意図的に再現しないケース（features.md の C08、C10）。
    /// picture-time：オリジナル版は EXIF の撮影日時を「Invalid DateTime」にする。document-basic：合成 PDF を読めない。
    /// </summary>
    private static readonly HashSet<string> KnownDeviations = ["picture-time", "document-basic"];

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var file in Directory.GetFiles(GoldenDir, "*.json").Order())
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
            {
                data.Add(Path.GetFileNameWithoutExtension(file), c.GetProperty("name").GetString()!);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesOriginal(string set, string name)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenDir, set + ".json")));
        var root = doc.RootElement;
        var c = root.GetProperty("cases").EnumerateArray().First(e => e.GetProperty("name").GetString() == name);
        var o = c.TryGetProperty("options", out var opt) ? opt : default;
        if (KnownDeviations.Contains(name))
        {
            return;
        }
        var tagKind = c.TryGetProperty("tag", out var tag) ? TagKinds.Value[tag.GetProperty("kind").GetInt32()] : null;

        string fixtureName = root.TryGetProperty("fixtureName", out var fn) ? fn.GetString()! : set == "wildcard-dots" ? "dots" : "basic";
        string folder = Materialized.GetOrAdd(fixtureName, _ => Materialize(fixtureName, root.GetProperty("fixture")));

        var items = FileCollector.Collect(folder, new CollectOptions
        {
            Files = Bool(o, "files", true),
            Folders = Bool(o, "folders", false),
            // タグ・リネームでは、選んだ種類の拡張子だけが一覧に出る
            Filter = tagKind is null ? Str(o, "filter", "*") : string.Join(';', tagKind.Extensions.Select(e => "*." + e)),
            Recursive = Bool(o, "recursive", false),
            Depth = Int(o, "depth", 0),
        });
        var (mode, search, replace, numberStart, script) = tagKind is not null
            // タグ・リネーム：書式の展開結果に拡張子を付けたものが新しい名前（TagRenamePage.CurrentOptions と同じ）
            ? (RenameMode.RegularExpression, "^.*$", c.GetProperty("format").GetString()! + @"\e", 0L, Str(o, "script", ""))
            : c.TryGetProperty("typical", out var typical)
            ? FromTypical(typical.GetProperty("index").GetInt32(), c.TryGetProperty("params", out var p) ? p : default)
            : (c.GetProperty("method").GetString() switch
            {
                "wildcard" => RenameMode.Wildcard,
                "regex" => RenameMode.RegularExpression,
                _ => RenameMode.CharacterRange,
            }, c.GetProperty("search").GetString()!, c.GetProperty("replace").GetString()!, (long)Int(o, "numberStart", 0), Str(o, "script", ""));

        var options = new RenameOptions
        {
            Mode = mode,
            Search = search,
            Replace = replace,
            CaseSensitive = Bool(o, "caseSensitive", false),
            CompleteMatchOnly = Bool(o, "completeMatchOnly", false),
            NumberStart = numberStart,
            NumberStep = Int(o, "numberStep", 1),
            NumberType = Str(o, "numberType", "decimal") switch
            {
                "hex" => NumberType.Hexadecimal,
                "alpha" => NumberType.Alphabetical,
                _ => NumberType.Decimal,
            },
            UseCreationTime = Bool(o, "useCreationTime", false),
            ResetNumberByFolder = Bool(o, "resetByFolder", false),
            Culture = CultureInfo.CurrentCulture,
            Random = new Random(0),
            // スクリプト（options.script に言語名、定型処理は Preset.ini の Script 列）。付属の Script.Preset.* を読み込む
            Script = script.Length == 0 ? null : Scripts.Value.Host(script.Equals("JScript", StringComparison.OrdinalIgnoreCase) ? ScriptLanguage.JScript : ScriptLanguage.VBScript).Evaluate,
            TagExpander = tagKind is null ? null : (item, format) => TagFormatter.Expand(
                format,
                TagReader.Read(item.FullPath),
                new TagFormatOptions { Culture = CultureInfo.CurrentCulture },
                key => Japanese.Value.Lookup(tagKind.Section, key),
                tagKind.Fields.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase).Contains),
        };

        var expected = c.GetProperty("results").EnumerateArray()
            .Select(r => Row(r.GetProperty("folder").GetString()!, r.GetProperty("name").GetString()!, r.GetProperty("newName").GetString()!))
            .ToList();

        // \P はフィクスチャの置き場所を含むので、オリジナル版で取ったときの場所に読み替える
        string originalRoot = Sanitize(Path.Combine(ReferenceFiles.RepoRoot, ".work", "fixtures", fixtureName));
        var actual = new RenameEngine(options).Preview(items)
            .Select(r => Row(
                Path.GetRelativePath(folder, r.Item.DirectoryPath).Replace('\\', '/') is var rel && rel == "." ? "" : rel,
                r.Item.Name,
                r.NewName.Replace(Sanitize(folder), originalRoot)))
            .ToList();

        if (RandomCases.Contains(name))
        {
            expected = expected.Select(MaskDigitsAfterArrow).ToList();
            actual = actual.Select(MaskDigitsAfterArrow).ToList();
        }
        if (tagKind is not null)
        {
            // オリジナル版は日本語のタグを文字化けさせる（U+FFFD を含む。features.md の C06）。その行は比べない
            var garbled = expected.Where(r => r.Contains('�')).Select(r => r[..r.IndexOf(" -> ", StringComparison.Ordinal)]).ToHashSet();
            expected = expected.Where(r => !garbled.Contains(r[..r.IndexOf(" -> ", StringComparison.Ordinal)])).ToList();
            actual = actual.Where(r => !garbled.Contains(r[..r.IndexOf(" -> ", StringComparison.Ordinal)])).ToList();
        }

        Assert.Equal(expected, actual);
    }

    /// <summary>オリジナル版の配布物にある Script.Preset.vbs／.js を読み込む（Script.vbs はない）。</summary>
    private static readonly Lazy<ScriptWorkspace> Scripts = new(() => new ScriptWorkspace(Path.GetDirectoryName(ReferenceFiles.Get("Script.Preset.vbs"))!));

    private static readonly Lazy<PresetFile> Presets = new(() => PresetFile.Load(ReferenceFiles.Get("Preset.ini")));

    private static readonly Lazy<LanguageStrings> Japanese = new(() => LanguageStrings.Load(ReferenceFiles.Get("Flexible Renamer.JA.Strings.ini")));

    private static readonly Lazy<IReadOnlyList<TagKind>> TagKinds = new(() => TagDefinitions.Load(ReferenceFiles.Get("TagDef.ini")));

    /// <summary>定型メニューのケース。パラメータは正解データを取ったときに入力した値で、ないものは既定値。</summary>
    private static (RenameMode, string, string, long, string) FromTypical(int index, JsonElement p)
    {
        var defaults = new TypicalParameters();
        var parameters = new TypicalParameters
        {
            AddString = Str(p, "add", defaults.AddString),
            StartNumber = long.Parse(Str(p, "start", defaults.StartNumber.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture),
            Digits = int.Parse(Str(p, "digits", defaults.Digits.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture),
            Position = int.Parse(Str(p, "position", defaults.Position.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture),
            Value = long.Parse(Str(p, "value", defaults.Value.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture),
            TimeFormat = Str(p, "timeFormat", defaults.TimeFormat),
            Source = Str(p, "source", defaults.Source),
            Destination = Str(p, "destination", defaults.Destination),
        };
        var e = TypicalTemplate.Expand(Presets.Value.Typicals[index], parameters, Japanese.Value);
        return (e.Mode, e.Search, e.Replace, e.NumberStart, e.Script);
    }

    private static string Row(string folder, string name, string newName) =>
        $"{(folder.Length > 0 ? folder + "/" : "")}{name} -> {newName}";

    /// <summary>プレビューでの置き換え（<c>\</c> → <c>%</c>、<c>:</c> → <c>;</c>）と同じ変換。</summary>
    private static string Sanitize(string path) => path.Replace('\\', '%').Replace(':', ';');

    private static bool Bool(JsonElement o, string key, bool fallback) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(key, out var v) ? v.GetBoolean() : fallback;

    private static int Int(JsonElement o, string key, int fallback) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(key, out var v) ? v.GetInt32() : fallback;

    private static string Str(JsonElement o, string key, string fallback) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(key, out var v) ? v.GetString()! : fallback;

    private static string MaskDigitsAfterArrow(string row)
    {
        int arrow = row.IndexOf(" -> ", StringComparison.Ordinal);
        return row[..arrow] + Regex.Replace(row[arrow..], "[0-9]", "#");
    }

    /// <summary>
    /// フィクスチャを一時フォルダの <c>fixtures\&lt;名前&gt;</c> に作る（<c>\F</c> が「fixtures」になるように）。
    /// タイムスタンプも正解データを取ったときと同じにする。
    /// </summary>
    private static string Materialize(string fixtureName, JsonElement fixture)
    {
        string dir = Path.Combine(Path.GetTempPath(), "FlexibleRenamerTests", Guid.NewGuid().ToString("N"), "fixtures", fixtureName);
        Directory.CreateDirectory(dir);

        var stamps = new List<(string Path, DateTime Modified, DateTime Created, bool IsDir)>();
        foreach (var e in fixture.EnumerateArray())
        {
            string rel = e.GetProperty("path").GetString()!;
            var modified = Parse(e.GetProperty("time").GetString()!);
            var created = e.TryGetProperty("created", out var cr) ? Parse(cr.GetString()!) : modified;
            string path = Path.Combine(dir, rel.TrimEnd('/'));
            if (rel.EndsWith('/'))
            {
                Directory.CreateDirectory(path);
                stamps.Add((path, modified, created, true));
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                // 合成ファイル（タグ付き）は中身も正解データに入っている
                File.WriteAllBytes(path, e.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                    ? Convert.FromBase64String(content.GetString()!)
                    : new byte[e.GetProperty("size").GetInt32()]);
                stamps.Add((path, modified, created, false));
            }
        }
        foreach (var (path, modified, created, isDir) in stamps.OrderByDescending(s => s.Path.Length))
        {
            if (isDir)
            {
                Directory.SetCreationTime(path, created);
                Directory.SetLastWriteTime(path, modified);
            }
            else
            {
                File.SetCreationTime(path, created);
                File.SetLastWriteTime(path, modified);
            }
        }
        return dir;
    }

    private static DateTime Parse(string s) => DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
