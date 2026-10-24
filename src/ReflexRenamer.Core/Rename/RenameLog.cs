using System.Globalization;
using System.Text;

namespace ReflexRenamer.Core.Rename;

/// <summary>ログに記録した1回分のリネーム操作。</summary>
public sealed record RenameLogEntry(
    DateTime Time,
    string Folder,
    string Search,
    string Replace,
    IReadOnlyList<(string From, string To)> Pairs,
    bool Completed);

/// <summary>
/// 元に戻す（UNDO）とエラー回復に使うログ（実行ディレクトリの <c>Rename.log</c>、UTF-16）。
/// </summary>
/// <remarks>
/// 形式（オリジナル版と同じ）:
/// <code>
/// このログファイルは UNDO，およびエラーリカバリに使用されます．   ← 説明（言語ファイルで訳される）
/// 2026/10/23 23:08:08,C:\フォルダ,検索,置換
/// C:\フォルダ\元の名前,C:\フォルダ\新しい名前
/// ...
/// Rename Completed Successfully!                                ← 正常に終わったときだけ書く
/// </code>
/// </remarks>
public static class RenameLog
{
    public const string CompletedMarker = "Rename Completed Successfully!";

    private const string TimeFormat = "yyyy/MM/dd HH:mm:ss";

    /// <summary>新しいログを書き始める。以前の内容は消える。</summary>
    public static StreamWriter Begin(string path, string description, DateTime time, string folder, string search, string replace)
    {
        var writer = new StreamWriter(path, append: false, Encoding.Unicode) { AutoFlush = true };
        writer.WriteLine(description);
        writer.WriteLine($"{time.ToString(TimeFormat, CultureInfo.InvariantCulture)},{folder},{search},{replace}");
        return writer;
    }

    public static void WritePair(StreamWriter writer, string from, string to) => writer.WriteLine($"{from},{to}");

    public static void Complete(StreamWriter writer) => writer.WriteLine(CompletedMarker);

    /// <summary>ログを読む。ファイルがない、または形式が不正なら null。</summary>
    public static RenameLogEntry? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        var lines = File.ReadAllLines(path, Encoding.Unicode);
        if (lines.Length < 2)
        {
            return null;
        }

        var header = SplitHeader(lines[1]);
        if (header is null)
        {
            return null;
        }

        var pairs = new List<(string, string)>();
        bool completed = false;
        foreach (var line in lines.Skip(2))
        {
            if (line == CompletedMarker)
            {
                completed = true;
                break;
            }
            int comma = SplitPairAt(line);
            if (comma < 0)
            {
                continue;
            }
            pairs.Add((line[..comma], line[(comma + 1)..]));
        }
        var (time, folder, search, replace) = header.Value;
        return new RenameLogEntry(time, folder, search, replace, pairs, completed);
    }

    private static (DateTime, string, string, string)? SplitHeader(string line)
    {
        var parts = line.Split(',', 4);
        if (parts.Length < 4 || !DateTime.TryParseExact(parts[0], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return null;
        }
        return (time, parts[1], parts[2], parts[3]);
    }

    /// <summary>
    /// 「元のパス,新しいパス」の区切りのカンマの位置。名前にカンマを含むことがあるので、
    /// 2つ目のドライブ指定（<c>,X:\</c>）または UNC（<c>,\\</c>）の直前を探す。
    /// </summary>
    private static int SplitPairAt(string line)
    {
        for (int i = line.IndexOf(','); i >= 0; i = line.IndexOf(',', i + 1))
        {
            var rest = line.AsSpan(i + 1);
            if ((rest.Length >= 3 && char.IsAsciiLetter(rest[0]) && rest[1] == ':' && rest[2] == '\\') || rest.StartsWith(@"\\"))
            {
                return i;
            }
        }
        return line.IndexOf(',');
    }
}
