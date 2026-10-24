using System.Runtime.InteropServices;

namespace FlexibleRenamer.Core.Rename;

/// <summary>実行する動作（リネームページ左下のコンボボックス）。</summary>
public enum RenameOperation
{
    Rename,
    MoveToFolder,
    CopyToFolder,
    ShortcutToFolder,
    HardLinkToFolder,
    RecycleBin,
}

/// <summary>名前の重複時の処理（DIALOG 168）。</summary>
public enum ConflictAction
{
    Retry,
    Skip,
    SkipAll,
    Overwrite,
    OverwriteAll,
    AutoNumber,
    Cancel,
}

/// <summary>エラー時の処理（DIALOG 130）。</summary>
public enum ErrorAction
{
    Retry,
    Skip,
    SkipAll,
    Cancel,
}

/// <summary>重複時に自動で付ける連番の設定。書式の <c>?</c> が連番になる。</summary>
public sealed record AutoNumberSettings(string Format = "(??)", bool AtEnd = true, long Start = 1, long Step = 1);

public sealed record ConflictDecision(ConflictAction Action, AutoNumberSettings? AutoNumber = null);

/// <summary>実行中にユーザーへ問い合わせるためのコールバック。UI がダイアログを出して答える。</summary>
public interface IRenameCallbacks
{
    ConflictDecision OnConflict(string folder, string sourceName, string destinationName);

    ErrorAction OnError(string folder, string sourceName, string destinationName, Exception error);

    /// <summary>進行状況（0〜1）。キャンセルされたら false を返す。</summary>
    bool OnProgress(double ratio) => true;
}

public sealed record ExecuteResult(int Succeeded, int Skipped, bool Cancelled);

/// <summary>
/// プレビューの結果に従ってファイルを処理し、元に戻すためのログを書く。
/// </summary>
public sealed class RenameExecutor(IRenameCallbacks callbacks)
{
    /// <summary>オプション「以下の設定で連番を付ける」。null なら重複のたびに問い合わせる。</summary>
    public AutoNumberSettings? AlwaysAutoNumber { get; init; }

    /// <summary>「別フォルダに…」の送り先。</summary>
    public string? DestinationFolder { get; init; }

    public RenameOperation Operation { get; init; } = RenameOperation.Rename;

    public ExecuteResult Execute(IReadOnlyList<PreviewRow> rows, StreamWriter? log)
    {
        int succeeded = 0;
        int skipped = 0;
        bool skipAllErrors = false;
        bool skipAllConflicts = false;
        bool overwriteAll = false;
        AutoNumberSettings? autoNumber = AlwaysAutoNumber;

        // フォルダの名前を先に変えると中のファイルのパスが変わるので、深い階層から処理する
        var ordered = rows
            .Select((r, i) => (Row: r, Index: i))
            .OrderByDescending(x => x.Row.Item.FullPath.Count(c => c == '\\'))
            .ThenBy(x => x.Index)
            .Select(x => x.Row)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            if (!callbacks.OnProgress((double)i / ordered.Count))
            {
                return new ExecuteResult(succeeded, skipped, true);
            }

            var row = ordered[i];
            string source = row.Item.FullPath;
            string destination = DestinationPath(row);
            if (Operation == RenameOperation.Rename && string.Equals(source, destination, StringComparison.Ordinal))
            {
                continue;
            }

            while (true)
            {
                try
                {
                    if (Operation != RenameOperation.RecycleBin && Exists(destination) && !IsSameEntry(source, destination))
                    {
                        if (overwriteAll)
                        {
                            Delete(destination);
                        }
                        else if (autoNumber is not null)
                        {
                            destination = NextFreeName(destination, autoNumber);
                        }
                        else if (skipAllConflicts)
                        {
                            skipped++;
                            break;
                        }
                        else
                        {
                            var decision = callbacks.OnConflict(row.Item.DirectoryPath, row.Item.Name, Path.GetFileName(destination));
                            switch (decision.Action)
                            {
                                case ConflictAction.Retry:
                                    continue;
                                case ConflictAction.Skip:
                                    skipped++;
                                    goto next;
                                case ConflictAction.SkipAll:
                                    skipAllConflicts = true;
                                    skipped++;
                                    goto next;
                                case ConflictAction.Overwrite:
                                    Delete(destination);
                                    break;
                                case ConflictAction.OverwriteAll:
                                    overwriteAll = true;
                                    Delete(destination);
                                    break;
                                case ConflictAction.AutoNumber:
                                    autoNumber = decision.AutoNumber ?? new AutoNumberSettings();
                                    destination = NextFreeName(destination, autoNumber);
                                    break;
                                case ConflictAction.Cancel:
                                    return new ExecuteResult(succeeded, skipped, true);
                            }
                        }
                    }

                    Perform(source, destination);
                    if (log is not null)
                    {
                        RenameLog.WritePair(log, source, destination);
                    }
                    succeeded++;
                    break;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or COMException)
                {
                    if (skipAllErrors)
                    {
                        skipped++;
                        break;
                    }
                    switch (callbacks.OnError(row.Item.DirectoryPath, row.Item.Name, Path.GetFileName(destination), e))
                    {
                        case ErrorAction.Retry:
                            continue;
                        case ErrorAction.Skip:
                            skipped++;
                            goto next;
                        case ErrorAction.SkipAll:
                            skipAllErrors = true;
                            skipped++;
                            goto next;
                        default:
                            return new ExecuteResult(succeeded, skipped, true);
                    }
                }
            }
        next:;
        }

        callbacks.OnProgress(1);
        if (log is not null)
        {
            RenameLog.Complete(log);
        }
        return new ExecuteResult(succeeded, skipped, false);
    }

    /// <summary>
    /// 新しい名前の行き先。名前に <c>/</c> や <c>..</c> があればフォルダを移動する（フォルダ振り分け）。
    /// 「別フォルダに…」の動作では送り先フォルダを基準にする。
    /// </summary>
    private string DestinationPath(PreviewRow row)
    {
        string baseDir = Operation == RenameOperation.Rename || DestinationFolder is null ? row.Item.DirectoryPath : DestinationFolder;
        string path = Path.GetFullPath(Path.Combine(baseDir, row.NewName.Replace('/', '\\')));
        return Operation == RenameOperation.ShortcutToFolder ? path + ".lnk" : path;
    }

    private void Perform(string source, string destination)
    {
        if (Operation != RenameOperation.RecycleBin)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        }
        bool isDir = Directory.Exists(source);
        switch (Operation)
        {
            case RenameOperation.Rename:
            case RenameOperation.MoveToFolder:
                if (isDir)
                {
                    Directory.Move(source, destination);
                }
                else
                {
                    File.Move(source, destination);
                }
                break;
            case RenameOperation.CopyToFolder:
                if (isDir)
                {
                    CopyDirectory(source, destination);
                }
                else
                {
                    File.Copy(source, destination);
                }
                break;
            case RenameOperation.ShortcutToFolder:
                Shell.CreateShortcut(destination, source);
                break;
            case RenameOperation.HardLinkToFolder:
                if (!NativeMethods.CreateHardLink(destination, source, 0))
                {
                    throw new IOException($"ハードリンクを作れない: {Marshal.GetLastPInvokeErrorMessage()}");
                }
                break;
            case RenameOperation.RecycleBin:
                Shell.MoveToRecycleBin(source);
                break;
        }
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>大文字小文字だけを変える場合など、行き先が元と同じものを指しているか。</summary>
    private static bool IsSameEntry(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    /// <summary>重複しない名前になるまで、書式の連番を付けて試す。</summary>
    public static string NextFreeName(string path, AutoNumberSettings settings)
    {
        string dir = Path.GetDirectoryName(path)!;
        string name = Path.GetFileName(path);
        string title = FileNameParts.Title(name);
        string ext = FileNameParts.Extension(name);
        for (long n = settings.Start; ; n += settings.Step)
        {
            string tag = NumberExpander.Expand(settings.Format, n, new RenameOptions());
            string candidate = Path.Combine(dir, settings.AtEnd ? title + tag + ext : tag + title + ext);
            if (!Exists(candidate))
            {
                return candidate;
            }
        }
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

    /// <summary>ログを読んで、記録された操作を逆順に元に戻す。戻せなかった件数を返す。</summary>
    public static int Undo(RenameLogEntry entry)
    {
        int failed = 0;
        foreach (var (from, to) in entry.Pairs.Reverse())
        {
            try
            {
                if (Directory.Exists(to))
                {
                    Directory.Move(to, from);
                }
                else
                {
                    File.Move(to, from);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }
        return failed;
    }
}

internal static partial class NativeMethods
{
    [LibraryImport("kernel32", EntryPoint = "CreateHardLinkW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CreateHardLink(string fileName, string existingFileName, nint securityAttributes);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ShFileOpStruct
    {
        public nint Hwnd;
        public uint Func;
        public string From;
        public string? To;
        public ushort Flags;
        public int AnyOperationsAborted;
        public nint NameMappings;
        public string? ProgressTitle;
    }

    [DllImport("shell32", CharSet = CharSet.Unicode)]
    internal static extern int SHFileOperation(ref ShFileOpStruct op);
}

/// <summary>シェルの機能（ショートカット作成、ごみ箱）。</summary>
internal static class Shell
{
    public static void CreateShortcut(string linkPath, string target)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("WScript.Shell が使えない");
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic link = shell.CreateShortcut(linkPath);
            link.TargetPath = target;
            link.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    public static void MoveToRecycleBin(string path)
    {
        const uint foDelete = 3;
        const ushort fofAllowUndo = 0x40;
        const ushort fofNoConfirmation = 0x10;
        const ushort fofSilent = 0x4;
        var op = new NativeMethods.ShFileOpStruct
        {
            Func = foDelete,
            From = path + "\0\0",
            Flags = fofAllowUndo | fofNoConfirmation | fofSilent,
        };
        int result = NativeMethods.SHFileOperation(ref op);
        if (result != 0)
        {
            throw new IOException($"ごみ箱に移動できない（エラー {result}）");
        }
    }
}
