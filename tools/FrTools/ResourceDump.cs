using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FrTools;

/// <summary>
/// exe のリソース（ダイアログ・メニュー・アクセラレータ・ツールバー）を読み、
/// 人が読む Markdown と機械が読む JSON に書き出す。
/// </summary>
internal static class ResourceDump
{
    private const uint LoadLibraryAsDatafile = 0x00000002;
    private const uint LoadLibraryAsImageResource = 0x00000020;

    private const int RtMenu = 4;
    private const int RtDialog = 5;
    private const int RtAccelerator = 9;
    private const int RtToolbar = 241; // MFC
    private const int RtString = 6;

    public static int Run(string exePath, string outDir)
    {
        nint module = LoadLibraryExW(exePath, 0, LoadLibraryAsDatafile | LoadLibraryAsImageResource);
        if (module == 0)
        {
            Console.Error.WriteLine($"読み込めない: {exePath}");
            return 1;
        }

        try
        {
            var dump = new ResourceSet(
                Read(module, RtDialog, DialogParser.Parse),
                Read(module, RtMenu, MenuParser.Parse),
                Read(module, RtAccelerator, ParseAccelerators),
                Read(module, RtToolbar, ParseToolbar),
                ReadStringTable(module));

            ExportImages(module, Path.Combine(Repo.Root, "src", "FlexibleRenamer", "Assets"));

            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "resources.json"),
                JsonSerializer.Serialize(dump, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            File.WriteAllText(Path.Combine(outDir, "resources.md"), ToMarkdown(dump));
            Console.WriteLine($"ダイアログ {dump.Dialogs.Count}、メニュー {dump.Menus.Count}、アクセラレータ {dump.Accelerators.Count}、ツールバー {dump.Toolbars.Count} を {outDir} に書き出した");
            return 0;
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    private static List<Named<T>> Read<T>(nint module, int type, Func<byte[], T> parse)
    {
        var names = new List<nint>();
        EnumResourceNamesW(module, type, (_, _, name, _) => { names.Add(name); return true; }, 0);

        var result = new List<Named<T>>();
        foreach (var name in names)
        {
            string id = IsIntResource(name) ? ((int)name).ToString() : Marshal.PtrToStringUni(name)!;
            nint res = FindResourceW(module, name, type);
            int size = (int)SizeofResource(module, res);
            nint ptr = LockResource(LoadResource(module, res));
            var bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            result.Add(new Named<T>(id, parse(bytes)));
        }
        return result.OrderBy(n => int.TryParse(n.Id, out var i) ? i : int.MaxValue).ToList();
    }

    private static bool IsIntResource(nint p) => ((ulong)p >> 16) == 0;

    /// <summary>
    /// 文字列テーブル。16 個ずつのブロックで、ID は (ブロック番号 - 1) * 16 + 位置。
    /// MFC ではコントロール ID と同じ番号の文字列がツールチップ、コマンド ID と同じ番号がステータスバーの説明になる。
    /// </summary>
    private static Dictionary<int, string> ReadStringTable(nint module)
    {
        var table = new Dictionary<int, string>();
        foreach (var block in Read(module, RtString, bytes => bytes))
        {
            int blockId = int.Parse(block.Id);
            int pos = 0;
            for (int i = 0; i < 16 && pos + 2 <= block.Value.Length; i++)
            {
                int length = BitConverter.ToUInt16(block.Value, pos);
                pos += 2;
                if (length > 0)
                {
                    table[(blockId - 1) * 16 + i] = Encoding.Unicode.GetString(block.Value, pos, length * 2);
                    pos += length * 2;
                }
            }
        }
        return table;
    }

    private const int RtBitmap = 2;
    private const int RtIcon = 3;
    private const int RtGroupIcon = 14;

    /// <summary>
    /// ビットマップ（ツールバーの画像など）を .bmp、アイコンを .ico として書き出す。移植版はこれを埋め込んで使う。
    /// </summary>
    private static void ExportImages(nint module, string assetsDir)
    {
        string bitmaps = Path.Combine(assetsDir, "Bitmaps");
        string icons = Path.Combine(assetsDir, "Icons");
        Directory.CreateDirectory(bitmaps);
        Directory.CreateDirectory(icons);

        foreach (var b in Read(module, RtBitmap, bytes => bytes))
        {
            // リソースには BITMAPFILEHEADER がないので付ける
            var dib = b.Value;
            int headerSize = BitConverter.ToInt32(dib, 0);
            int bitCount = BitConverter.ToUInt16(dib, 14);
            int colors = BitConverter.ToInt32(dib, 32);
            if (colors == 0 && bitCount <= 8) colors = 1 << bitCount;
            int offset = 14 + headerSize + colors * 4;
            using var file = File.Create(Path.Combine(bitmaps, b.Id + ".bmp"));
            using var w = new BinaryWriter(file);
            w.Write((ushort)0x4D42);
            w.Write(14 + dib.Length);
            w.Write(0);
            w.Write(offset);
            w.Write(dib);
        }

        var images = Read(module, RtIcon, bytes => bytes).ToDictionary(i => i.Id, i => i.Value);
        foreach (var g in Read(module, RtGroupIcon, bytes => bytes))
        {
            // GRPICONDIR の各項目（14バイト、最後が RT_ICON の ID）を ICONDIRENTRY（16バイト、最後がオフセット）に直す
            var dir = g.Value;
            int count = BitConverter.ToUInt16(dir, 4);
            var parts = new List<byte[]>();
            using var file = File.Create(Path.Combine(icons, g.Id + ".ico"));
            using var w = new BinaryWriter(file);
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)count);
            int offset = 6 + count * 16;
            for (int i = 0; i < count; i++)
            {
                int e = 6 + i * 14;
                string iconId = BitConverter.ToUInt16(dir, e + 12).ToString();
                var image = images.TryGetValue(iconId, out var img) ? img : [];
                w.Write(dir, e, 12);
                w.Write(offset);
                parts.Add(image);
                offset += image.Length;
            }
            foreach (var p in parts)
            {
                w.Write(p);
            }
        }
    }

    private static List<Accelerator> ParseAccelerators(byte[] b)
    {
        var list = new List<Accelerator>();
        for (int i = 0; i + 8 <= b.Length; i += 8)
        {
            int flags = BitConverter.ToUInt16(b, i);
            int key = BitConverter.ToUInt16(b, i + 2);
            int cmd = BitConverter.ToUInt16(b, i + 4);
            list.Add(new Accelerator(cmd, DescribeKey(flags, key)));
            if ((flags & 0x80) != 0)
            {
                break;
            }
        }
        return list;
    }

    private static string DescribeKey(int flags, int key)
    {
        var sb = new StringBuilder();
        if ((flags & 0x08) != 0) sb.Append("Ctrl+");
        if ((flags & 0x04) != 0) sb.Append("Shift+");
        if ((flags & 0x10) != 0) sb.Append("Alt+");
        // FVIRTKEY が立っていれば仮想キーコード
        sb.Append((flags & 0x01) != 0 ? ((System.Windows.Forms.Keys)key).ToString() : ((char)key).ToString());
        return sb.ToString();
    }

    private static Toolbar ParseToolbar(byte[] b)
    {
        int width = BitConverter.ToUInt16(b, 2);
        int height = BitConverter.ToUInt16(b, 4);
        int count = BitConverter.ToUInt16(b, 6);
        var ids = Enumerable.Range(0, count).Select(i => (int)BitConverter.ToUInt16(b, 8 + i * 2)).ToList();
        return new Toolbar(width, height, ids);
    }

    private static string ToMarkdown(ResourceSet dump)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# オリジナル exe のリソース一覧");
        sb.AppendLine();
        sb.AppendLine("`dotnet run --project tools/FrTools -- res-dump` で生成。手で編集しない。座標はダイアログ単位（DLU）。");
        sb.AppendLine();

        sb.AppendLine("## ダイアログ");
        foreach (var d in dump.Dialogs)
        {
            var dlg = d.Value;
            sb.AppendLine();
            sb.AppendLine($"### DIALOG {d.Id} 「{dlg.Title}」");
            sb.AppendLine();
            sb.AppendLine($"- サイズ {dlg.Width}x{dlg.Height}、スタイル 0x{dlg.Style:X8}、フォント {dlg.Font}");
            sb.AppendLine();
            sb.AppendLine("| ID | クラス | テキスト | x,y,w,h | スタイル |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var c in dlg.Controls)
            {
                sb.AppendLine($"| {c.Id} | {c.Class} | {Escape(c.Text)} | {c.X},{c.Y},{c.Width},{c.Height} | 0x{c.Style:X8} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## メニュー");
        foreach (var m in dump.Menus)
        {
            sb.AppendLine();
            sb.AppendLine($"### MENU {m.Id}");
            sb.AppendLine();
            AppendMenu(sb, m.Value, 0);
        }

        sb.AppendLine();
        sb.AppendLine("## アクセラレータ");
        foreach (var a in dump.Accelerators)
        {
            sb.AppendLine();
            sb.AppendLine($"### ACCELERATOR {a.Id}");
            sb.AppendLine();
            foreach (var k in a.Value)
            {
                sb.AppendLine($"- {k.Key} → コマンド {k.Command}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## 文字列テーブル");
        sb.AppendLine();
        sb.AppendLine("ID がコントロール ID と同じものはツールチップ、コマンド ID と同じものはステータスバーの説明（`\n` の後ろがツールチップ）。");
        sb.AppendLine();
        sb.AppendLine("| ID | 文字列 |");
        sb.AppendLine("|---|---|");
        foreach (var (id, text) in dump.Strings.OrderBy(kv => kv.Key))
        {
            sb.AppendLine($"| {id} | {Escape(text)} |");
        }

        sb.AppendLine();
        sb.AppendLine("## ツールバー");
        foreach (var t in dump.Toolbars)
        {
            sb.AppendLine();
            sb.AppendLine($"### TOOLBAR {t.Id}（ボタン {t.Value.Width}x{t.Value.Height}）");
            sb.AppendLine();
            sb.AppendLine("コマンド: " + string.Join(", ", t.Value.Commands.Select(c => c == 0 ? "|" : c.ToString())));
        }

        return sb.ToString();
    }

    private static void AppendMenu(StringBuilder sb, List<MenuItem> items, int depth)
    {
        foreach (var item in items)
        {
            string indent = new(' ', depth * 2);
            if (item.Separator)
            {
                sb.AppendLine($"{indent}- ----");
                continue;
            }
            string cmd = item.Children is null ? $" → コマンド {item.Id}" : "";
            sb.AppendLine($"{indent}- {Escape(item.Text)}{cmd}");
            if (item.Children is not null)
            {
                AppendMenu(sb, item.Children, depth + 1);
            }
        }
    }

    private static string Escape(string s) => s.Replace("|", "\\|").Replace("\t", "\\t").Replace("\r", "").Replace("\n", "\\n");

    private delegate bool EnumResNameProc(nint module, nint type, nint name, nint param);

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadLibraryExW(string path, nint file, uint flags);

    [DllImport("kernel32")]
    private static extern bool FreeLibrary(nint module);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern bool EnumResourceNamesW(nint module, nint type, EnumResNameProc proc, nint param);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern nint FindResourceW(nint module, nint name, nint type);

    [DllImport("kernel32")]
    private static extern uint SizeofResource(nint module, nint res);

    [DllImport("kernel32")]
    private static extern nint LoadResource(nint module, nint res);

    [DllImport("kernel32")]
    private static extern nint LockResource(nint data);
}

internal sealed record Named<T>(string Id, T Value);

internal sealed record ResourceSet(
    List<Named<Dialog>> Dialogs,
    List<Named<List<MenuItem>>> Menus,
    List<Named<List<Accelerator>>> Accelerators,
    List<Named<Toolbar>> Toolbars,
    Dictionary<int, string> Strings);

internal sealed record Accelerator(int Command, string Key);

internal sealed record Toolbar(int Width, int Height, List<int> Commands);
