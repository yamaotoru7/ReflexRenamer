using System.Globalization;
using FlexibleRenamer.Core.Rename;

namespace FlexibleRenamer.Core.NumberedObjects;

public enum NumberedObjectKind
{
    EmptyFile,
    CopiedFile,
    EmptyFolder,
    CopiedFolder,
}

public sealed record NumberedObjectOptions
{
    public NumberedObjectKind Kind { get; init; } = NumberedObjectKind.EmptyFile;

    /// <summary>空ファイルの大きさ。「1K」「2M」「3G」も可。</summary>
    public string Size { get; init; } = "0";

    /// <summary>コピー元（ファイルコピー、フォルダコピー）。</summary>
    public string Source { get; init; } = "";

    /// <summary>名前。<c>?</c> の並びが連番になる（桁数は <c>?</c> の個数）。</summary>
    public string Name { get; init; } = "";

    public int Amount { get; init; } = 10;

    public long Start { get; init; }

    public long Step { get; init; } = 1;
}

/// <summary>
/// 連番オブジェクトページの処理：指定フォルダに連番付きのファイル・フォルダを作る。
/// </summary>
public static class NumberedObjectGenerator
{
    /// <summary>作られる名前の一覧（プレビュー）。名前に <c>?</c> がなければ番号だけの名前になる（実測）。</summary>
    public static List<string> Names(NumberedObjectOptions o)
    {
        var names = new List<string>(Math.Max(o.Amount, 0));
        string pattern = o.Name.Contains('?') ? o.Name : o.Name + "?";
        long n = o.Start;
        for (int i = 0; i < o.Amount; i++, n += o.Step)
        {
            names.Add(NumberExpander.Expand(pattern.Replace("*", "\u0001", StringComparison.Ordinal), n, new RenameOptions()).Replace("\u0001", "*", StringComparison.Ordinal));
        }
        return names;
    }

    /// <summary>「1K」のような表記をバイト数にする。解釈できなければ 0。</summary>
    public static long ParseSize(string text)
    {
        text = text.Trim().ToUpperInvariant();
        long multiplier = 1;
        if (text.Length > 0 && "KMG".Contains(text[^1]))
        {
            multiplier = text[^1] switch { 'K' => 1024L, 'M' => 1024L * 1024, _ => 1024L * 1024 * 1024 };
            text = text[..^1];
        }
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v * multiplier : 0;
    }

    /// <summary>作成する。失敗した件数を返す。</summary>
    public static int Create(string folder, NumberedObjectOptions o, Func<double, bool>? progress = null)
    {
        var names = Names(o);
        int failed = 0;
        long size = ParseSize(o.Size);
        for (int i = 0; i < names.Count; i++)
        {
            if (progress is not null && !progress((double)i / names.Count))
            {
                break;
            }
            string path = Path.Combine(folder, names[i]);
            try
            {
                switch (o.Kind)
                {
                    case NumberedObjectKind.EmptyFile:
                        using (var f = new FileStream(path, FileMode.CreateNew))
                        {
                            f.SetLength(size);
                        }
                        break;
                    case NumberedObjectKind.CopiedFile:
                        File.Copy(o.Source, path);
                        break;
                    case NumberedObjectKind.EmptyFolder:
                        Directory.CreateDirectory(path);
                        break;
                    case NumberedObjectKind.CopiedFolder:
                        CopyDirectory(o.Source, path);
                        break;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }
        progress?.Invoke(1);
        return failed;
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
        foreach (var sub in Directory.GetDirectories(from))
        {
            CopyDirectory(sub, Path.Combine(to, Path.GetFileName(sub)));
        }
    }
}
