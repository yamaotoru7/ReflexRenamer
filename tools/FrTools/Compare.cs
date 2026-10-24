using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.RegularExpressions;

namespace FrTools;

/// <summary>
/// 同じシナリオを両方のアプリで実行した結果を比べ、<c>.work/out/&lt;シナリオ&gt;/compare/</c> に差分を書き出す。
/// </summary>
internal static class Compare
{
    /// <summary>この値以下の色差は同じ色とみなす（アンチエイリアスの揺れを吸収する）。</summary>
    private const int Tolerance = 24;

    public static string Run(string scenario)
    {
        string o = Scenario.OutputDir(scenario, AppKind.Original);
        string c = Scenario.OutputDir(scenario, AppKind.Clone);
        string outDir = Path.Combine(Sandbox.OutDir, scenario, "compare");
        Directory.CreateDirectory(outDir);

        var report = new StringBuilder($"# {scenario}\n\n");

        // 合否は、一覧の中身・メニュー・ファイルの結果などテキストの記録の一致だけで決める。
        // 画面（画像・UI ツリー）は見た目を Windows 11 風に変えたので差が出るのが前提で、参考として残す（docs/ui-redesign.md）
        var failed = new List<string>();
        foreach (var file in Directory.GetFiles(o).Select(Path.GetFileName).Order())
        {
            string a = Path.Combine(o, file!);
            string b = Path.Combine(c, file!);
            bool visual = file!.EndsWith(".png") || file.EndsWith(".tree.txt");
            if (!File.Exists(b))
            {
                report.AppendLine($"- {file}: 移植版の出力がない");
                if (!visual) failed.Add(file);
                continue;
            }

            if (file.EndsWith(".png"))
            {
                string side = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".side.png");
                double ratio = DiffImages(a, b, side);
                report.AppendLine($"- {file}（参考）: 画素差 {ratio:P2} → {side}");
            }
            else if (file.EndsWith(".tree.txt"))
            {
                report.AppendLine($"- {file}（参考）: {DiffTrees(a, b, Path.Combine(outDir, file))}");
            }
            else
            {
                string result = DiffText(a, b, Path.Combine(outDir, file + ".diff"));
                report.AppendLine($"- {file}: {result}");
                if (result != "一致") failed.Add(file);
            }
        }

        report.AppendLine();
        report.AppendLine(failed.Count == 0
            ? "**合否: OK**（テキストの記録はすべて一致。画像と UI ツリーは参考）"
            : $"**合否: NG**（{string.Join("、", failed)}）");

        File.WriteAllText(Path.Combine(outDir, "report.md"), report.ToString());
        return report.ToString();
    }

    /// <summary>
    /// 左にオリジナル、中央に移植版、右に差分（違う画素を赤、同じ画素を薄いグレー）を並べた画像を作る。
    /// 戻り値は違う画素の割合。
    /// </summary>
    public static double DiffImages(string pathA, string pathB, string outPath)
    {
        using var a = new Bitmap(pathA);
        using var b = new Bitmap(pathB);
        int w = Math.Max(a.Width, b.Width);
        int h = Math.Max(a.Height, b.Height);

        using var diff = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        long different = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inA = x < a.Width && y < a.Height;
                bool inB = x < b.Width && y < b.Height;
                if (!inA || !inB)
                {
                    diff.SetPixel(x, y, Color.Magenta);
                    different++;
                    continue;
                }
                var ca = a.GetPixel(x, y);
                var cb = b.GetPixel(x, y);
                if (Math.Abs(ca.R - cb.R) > Tolerance || Math.Abs(ca.G - cb.G) > Tolerance || Math.Abs(ca.B - cb.B) > Tolerance)
                {
                    diff.SetPixel(x, y, Color.Red);
                    different++;
                }
                else
                {
                    int g = 200 + (ca.R + ca.G + ca.B) / 3 * 55 / 255;
                    diff.SetPixel(x, y, Color.FromArgb(g, g, g));
                }
            }
        }

        const int gap = 8;
        using var side = new Bitmap(w * 3 + gap * 2, h);
        using (var g = Graphics.FromImage(side))
        {
            g.Clear(Color.Black);
            g.DrawImage(a, 0, 0, a.Width, a.Height);
            g.DrawImage(b, w + gap, 0, b.Width, b.Height);
            g.DrawImage(diff, (w + gap) * 2, 0, w, h);
        }
        side.Save(outPath, ImageFormat.Png);
        return (double)different / (w * h);
    }

    /// <summary>
    /// UI ツリーを、座標を除いた「種類・名前・ID」で比べる。
    /// 移植版にない要素と、移植版にだけある要素を書き出す。
    /// </summary>
    private static string DiffTrees(string pathA, string pathB, string outPath)
    {
        var a = Keys(pathA);
        var b = Keys(pathB);
        var missing = a.Except(b).ToList();
        var extra = b.Except(a).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("## 移植版にない要素");
        missing.ForEach(m => sb.AppendLine(m));
        sb.AppendLine();
        sb.AppendLine("## 移植版にだけある要素");
        extra.ForEach(m => sb.AppendLine(m));
        File.WriteAllText(outPath, sb.ToString());
        return $"不足 {missing.Count}、余分 {extra.Count} → {outPath}";
    }

    private static readonly Regex Coordinates = new(@" \[-?\d+,-?\d+,\d+,\d+\]| offscreen| class=\S+", RegexOptions.Compiled);

    private static HashSet<string> Keys(string path) =>
        File.ReadAllLines(path)
            .Select(l => Coordinates.Replace(l.Trim(), ""))
            // フォルダツリーの中身は PC ごとに違うので比べない
            .Where(l => !l.StartsWith("TreeItem") && !l.StartsWith("ScrollBar") && !l.StartsWith("Thumb") && !l.StartsWith("Button \"1 "))
            .ToHashSet();

    private static string DiffText(string pathA, string pathB, string outPath)
    {
        var a = File.ReadAllLines(pathA);
        var b = File.ReadAllLines(pathB);
        var sb = new StringBuilder();
        int n = Math.Max(a.Length, b.Length);
        int count = 0;
        for (int i = 0; i < n; i++)
        {
            string la = i < a.Length ? a[i] : "<なし>";
            string lb = i < b.Length ? b[i] : "<なし>";
            if (la != lb)
            {
                count++;
                sb.AppendLine($"@{i + 1}\n- {la}\n+ {lb}");
            }
        }
        if (count == 0)
        {
            if (File.Exists(outPath)) File.Delete(outPath);
            return "一致";
        }
        File.WriteAllText(outPath, sb.ToString());
        return $"{count} 行違う → {outPath}";
    }
}
