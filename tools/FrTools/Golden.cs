using System.Text.Json;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FrTools;

/// <summary>
/// オリジナル版のプレビュー一覧から、リネーム処理の正解データを取る。
/// <c>tools/FrTools/golden/&lt;セット&gt;.json</c> のケースを順に入力し、結果を
/// <c>tests/FlexibleRenamer.Core.Tests/Golden/&lt;セット&gt;.json</c> に書く。
/// </summary>
/// <remarks>
/// 入力の形式:
/// <code>
/// {
///   "fixture": "basic",
///   "setup": [ ...シナリオと同じステップ... ],      // 最初に1回だけ実行
///   "cases": [
///     { "name": "wc-basic", "method": "wildcard", "search": "*.*", "replace": "\\1_x.\\2",
///       "steps": [ ...このケースの前に実行するステップ（任意）... ] }
///   ]
/// }
/// </code>
/// method は wildcard / regex / translation。
/// 定型メニューのケースは method の代わりに
/// <c>"typical": { "index": 0, "label": "文字列（日時）+連番" }, "params": { "add": "IMG_", "start": 1, "digits": 2 }</c>
/// と書く（index は Preset.ini の [Typical] の順番、label はツリーの表示名）。
/// params は add / start / digits / position / value / timeFormat / source / destination。値は次のケースにも残る。
/// </remarks>
internal static class Golden
{
    private static readonly Dictionary<string, string> MethodRadio = new()
    {
        ["wildcard"] = "id:1037",
        ["regex"] = "id:1038",
        ["translation"] = "id:1039",
    };

    /// <summary>定型メニューのパラメータ名と、入力欄（DIALOG 146 のコントロール）の対応。</summary>
    /// <remarks>ID はダイアログ間で重複する（1025 は「高度なリネーム」チェックボックスでもある）ので、種類も指定する。</remarks>
    private static readonly Dictionary<string, string> TypicalParamField = new()
    {
        ["add"] = "id:1018,type:ComboBox",
        ["start"] = "id:1065,type:Edit",
        ["digits"] = "id:1019,type:Edit",
        ["position"] = "id:1031,type:Edit",
        ["value"] = "id:1066,type:Edit",
        ["timeFormat"] = "id:1045,type:ComboBox",
        ["source"] = "id:1025,type:ComboBox",
        ["destination"] = "id:1027,type:ComboBox",
    };

    public static string InputDir => Path.Combine(Repo.Root, "tools", "FrTools", "golden");

    public static string OutputDir => Path.Combine(Repo.Root, "tests", "FlexibleRenamer.Core.Tests", "Golden");

    public static IEnumerable<string> All() =>
        Directory.GetFiles(InputDir, "*.json").Select(Path.GetFileNameWithoutExtension).Order()!;

    public static int Run(string set)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(InputDir, set + ".json")),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        string fixture = root.TryGetProperty("fixture", out var f) ? f.GetString()! : "basic";

        Sandbox.ResetOriginal();
        string folder = Sandbox.ResetFixture(fixture);
        string scratch = Path.Combine(Sandbox.OutDir, "golden", set);
        Directory.CreateDirectory(scratch);

        using var session = AppSession.Launch(AppKind.Original, folder);
        session.Settle(1500);

        if (root.TryGetProperty("setup", out var setup))
        {
            RunSteps(session, setup, folder, scratch);
        }

        var cases = new JsonArray();
        foreach (var c in root.GetProperty("cases").EnumerateArray())
        {
            string name = c.GetProperty("name").GetString()!;
            if (c.TryGetProperty("steps", out var steps))
            {
                RunSteps(session, steps, folder, scratch);
            }

            if (c.TryGetProperty("tag", out var tag))
            {
                // タグ・リネーム：種類の一覧（ListBox 1088）で項目を選び、書式を入力する
                int kind = tag.GetProperty("kind").GetInt32();
                // オリジナル版の一覧はオーナードローで、項目のテキストの位置をマウスでクリックしないと切り替わらない
                RunStep(session, new { @do = "click", target = "id:1088,type:List", at = new[] { 100, 11 + 18 * kind } }, folder, scratch);
                session.Settle(800);
                TypeInto(session, "id:1019,type:ComboBox", c.GetProperty("format").GetString()!);
            }
            else if (c.TryGetProperty("typical", out var typical))
            {
                // 定型メニュー：ツリーの項目を選び、パラメータ欄に入力する
                RunStep(session, new { @do = "treeSelect", target = "id:1014", item = typical.GetProperty("label").GetString()! }, folder, scratch);
                if (c.TryGetProperty("params", out var parameters))
                {
                    foreach (var p in parameters.EnumerateObject())
                    {
                        TypeInto(session, TypicalParamField[p.Name], p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetRawText() : p.Value.GetString()!);
                    }
                }
            }
            else
            {
                string method = c.GetProperty("method").GetString()!;
                RunStep(session, new { @do = "click", target = MethodRadio[method] }, folder, scratch);
                TypeInto(session, "id:1010", c.GetProperty("search").GetString()!);
                TypeInto(session, "id:1019", c.GetProperty("replace").GetString()!);
            }
            session.Settle(600);

            var node = JsonNode.Parse(c.GetRawText())!.AsObject();
            node.Remove("steps");
            node["results"] = ReadResults(new Selector("id:1000,type:List").Find(session), folder);
            cases.Add(node);
            Console.WriteLine($"  {name}");
        }

        var output = new JsonObject
        {
            ["source"] = $"tools/FrTools/golden/{set}.json から `FrTools golden {set}` で生成。手で編集しない。",
            ["fixtureName"] = fixture,
            ["fixture"] = FixtureJson(fixture),
            ["cases"] = cases,
        };
        Directory.CreateDirectory(OutputDir);
        string path = Path.Combine(OutputDir, set + ".json");
        File.WriteAllText(path, output.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
        Console.WriteLine(path);
        return 0;
    }

    private static void RunSteps(AppSession session, JsonElement steps, string folder, string outDir)
    {
        foreach (var step in steps.EnumerateArray())
        {
            Scenario.ExecuteWithRetry(session, step, step.GetProperty("do").GetString()!, folder, outDir);
        }
    }

    private static void RunStep(AppSession session, object step, string folder, string outDir)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(step));
        Scenario.ExecuteWithRetry(session, doc.RootElement, doc.RootElement.GetProperty("do").GetString()!, folder, outDir);
    }

    private static void TypeInto(AppSession session, string selector, string text) =>
        RunStep(session, new { @do = "setText", target = selector, text }, "", "");

    /// <summary>一覧の各行を「フォルダ（フィクスチャからの相対）・現在の名前・新しい名前」にする。</summary>
    private static JsonArray ReadResults(AutomationElement list, string folder)
    {
        var headers = list.FindAllDescendants(cf => cf.ByControlType(ControlType.HeaderItem)).Select(h => h.Name).ToList();
        if (!headers.Contains("新しい名前"))
        {
            return [];
        }
        int nameCol = headers.IndexOf("現在の名前");
        int newCol = headers.IndexOf("新しい名前");
        int folderCol = headers.IndexOf("フォルダ");

        var rows = new JsonArray();
        foreach (var item in list.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem)))
        {
            var cells = item.FindAllChildren();
            string dir = Path.GetRelativePath(folder, cells[folderCol].Name ?? "").Replace('\\', '/');
            rows.Add(new JsonObject
            {
                ["folder"] = dir == "." ? "" : dir,
                ["name"] = cells[nameCol].Name,
                ["newName"] = cells[newCol].Name,
            });
        }
        return rows;
    }

    private static JsonArray FixtureJson(string fixture)
    {
        var array = new JsonArray();
        foreach (var e in Fixtures.All[fixture])
        {
            array.Add(new JsonObject
            {
                ["path"] = e.Path,
                ["time"] = e.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                ["created"] = e.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ["size"] = e.Size,
                // 合成ファイル（タグ付き mp3 など）は中身も入れて、テストで同じファイルを作れるようにする
                ["content"] = e.Content is null ? null : Convert.ToBase64String(e.Content),
            });
        }
        return array;
    }
}
