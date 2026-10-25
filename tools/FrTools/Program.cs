using FrTools;

string root = Repo.Root;
string command = args.Length > 0 ? args[0] : "";

try
{
    return command switch
    {
        "res-dump" => ResourceDump.Run(Path.Combine(root, "FlexRenamer", "Flexible Renamer.exe"), Path.Combine(root, "docs", "spec")),
        "sandbox" => SandboxCommand(),
        "snap" => Snap(args.Skip(1).ToArray()),
        "run" => RunScenarios(args.ElementAtOrDefault(1), args.ElementAtOrDefault(2) ?? "both", compare: false),
        "compare" => RunScenarios(args.ElementAtOrDefault(1), args.ElementAtOrDefault(2) ?? "both", compare: true),
        "diff" => DiffImages(args),
        "golden" => RunGolden(args.ElementAtOrDefault(1)),
        _ => Usage(),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"エラー: {ex.Message}{(ex.InnerException is { } inner ? " ← " + inner.GetType().Name + ": " + inner.Message : "")}");
    return 1;
}

static int SandboxCommand()
{
    Sandbox.ResetOriginal();
    foreach (var name in Fixtures.All.Keys)
    {
        Sandbox.ResetFixture(name);
    }
    Console.WriteLine($"オリジナル版: {Sandbox.OriginalDir}");
    Console.WriteLine($"フィクスチャ: {Sandbox.FixturesDir}");
    return 0;
}

// アプリを起動して、メインウィンドウのスクリーンショットと UI ツリーを取る
static int Snap(string[] args)
{
    var kind = ParseApp(args.ElementAtOrDefault(0));
    string fixture = args.ElementAtOrDefault(1) ?? "basic";
    // 3つ目の引数で、フィクスチャの代わりに任意のフォルダを開ける（フォルダツリーの確認など）
    string folder = args.ElementAtOrDefault(2) ?? Sandbox.ResetFixture(fixture);
    string outDir = Path.Combine(Sandbox.OutDir, "snap", kind.ToString().ToLowerInvariant());

    using var session = AppSession.Launch(kind, folder);
    session.Settle(1500);
    session.Capture(session.Main, Path.Combine(outDir, "main.png"));
    File.WriteAllText(Path.Combine(outDir, "main.tree.txt"), UiTree.Dump(session.Main));
    Console.WriteLine(outDir);
    return 0;
}

// シナリオを実行する。名前を省略するとすべてのシナリオ
static int RunScenarios(string? name, string apps, bool compare)
{
    var names = name is null or "all" ? Scenario.All().ToList() : [name];
    var kinds = apps == "both" ? [AppKind.Original, AppKind.Clone] : new[] { ParseApp(apps) };
    int failures = 0;
    foreach (var n in names)
    {
        foreach (var kind in kinds)
        {
            try
            {
                Retry.Run($"{n} [{kind}]", () => Scenario.Run(n, kind));
                Console.WriteLine($"{n} [{kind}] → {Scenario.OutputDir(n, kind)}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine($"{n} [{kind}] 失敗: {ex.Message}");
            }
        }
        if (compare)
        {
            Console.WriteLine(Compare.Run(n));
        }
    }
    return failures == 0 ? 0 : 1;
}

static int RunGolden(string? set)
{
    foreach (var s in set is null or "all" ? Golden.All().ToList() : [set])
    {
        Console.WriteLine(s);
        Retry.Run(s, () => Golden.Run(s));
    }
    return 0;
}

static int DiffImages(string[] args)
{
    if (args.Length < 4) return Usage();
    double ratio = Compare.DiffImages(args[1], args[2], args[3]);
    Console.WriteLine($"画素差 {ratio:P2} → {args[3]}");
    return 0;
}

static AppKind ParseApp(string? s) => s?.ToLowerInvariant() switch
{
    "original" or "o" => AppKind.Original,
    "clone" or "c" => AppKind.Clone,
    _ => throw new ArgumentException("アプリは original か clone を指定する"),
};

static int Usage()
{
    Console.WriteLine("""
        使い方: dotnet run --project tools/FrTools -- <コマンド>

          res-dump                       オリジナル exe のリソースを docs/spec/resources.{md,json} に書き出す
          sandbox                        オリジナル版の作業コピーとフィクスチャを .work/ に作り直す
          snap <original|clone> [fix]    起動してメイン画面のスクリーンショットと UI ツリーを .work/out/snap/ に保存
          run [シナリオ|all] [original|clone|both]
                                         tools/FrTools/scenarios/<シナリオ>.json を実行し、結果を .work/out/<シナリオ>/<アプリ>/ に保存
          compare [シナリオ|all] [both|clone|original]
                                         両方（または片方だけ）で実行し、差分を .work/out/<シナリオ>/compare/ に書き出す
          diff <a.png> <b.png> <out.png> 2枚の画像を比べ、横並び＋差分の画像を作る
          golden [セット|all]             tools/FrTools/golden/<セット>.json の各ケースをオリジナル版に入力し、
                                         プレビュー結果を tests/ReflexRenamer.Core.Tests/Golden/ に保存
        """);
    return 1;
}
