using System.Runtime.InteropServices;

namespace ReflexRenamer.Core.Tags;

/// <summary>
/// エクスプローラーの「詳細」の列（<c>Shell.Application</c> の <c>Folder.GetDetailsOf</c>）。
/// タグ・リネームの「ファイルプロパティ情報を使う」で、列番号から表示名と値を引く。
/// 列番号と表示名は Windows のバージョンで決まり、フォルダには依らない。
/// </summary>
public sealed class ShellColumns
{
    /// <summary>プロパティストアを開けないファイル（壊れた mp3 など）の値。オリジナル版と同じ文字列。</summary>
    public const string UnknownTag = "UnknownTag";

    /// <summary>調べる列番号の上限。Windows 11 では 330 番台まである。</summary>
    private const int MaxColumn = 400;

    private readonly dynamic _shell;
    private readonly Dictionary<string, dynamic> _folders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<int, string>> _values = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<(int Index, string Name)>? _names;

    private ShellColumns(dynamic shell) => _shell = shell;

    /// <summary>作れないとき（COM が使えない環境）は null。</summary>
    public static ShellColumns? Create()
    {
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            return type is null ? null : new ShellColumns(Activator.CreateInstance(type)!);
        }
        catch (Exception e) when (e is COMException or InvalidCastException or MissingMethodException)
        {
            return null;
        }
    }

    /// <summary>名前のある列を番号順に。</summary>
    public IReadOnlyList<(int Index, string Name)> Names
    {
        get
        {
            if (_names is null)
            {
                var list = new List<(int, string)>();
                try
                {
                    dynamic desktop = _shell.NameSpace(0);
                    for (int i = 0; i <= MaxColumn; i++)
                    {
                        string name = (string?)desktop.GetDetailsOf(null, i) ?? "";
                        if (name.Length > 0)
                        {
                            list.Add((i, name));
                        }
                    }
                }
                catch (COMException)
                {
                }
                _names = list;
            }
            return _names;
        }
    }

    /// <summary>列番号の表示名。ない番号は null。</summary>
    public string? NameOf(int column) => Names.FirstOrDefault(n => n.Index == column).Name;

    /// <summary>表示名から列番号（大文字小文字を区別しない）。</summary>
    public int? ColumnOf(string name)
    {
        foreach (var (index, n) in Names)
        {
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return null;
    }

    /// <summary>
    /// ファイルの列の値（エクスプローラーの表示と同じ文字列。日付には LRM などの制御文字が入る）。
    /// プロパティストアを開けないファイルでは <see cref="UnknownTag"/>（オリジナル版の挙動）。
    /// </summary>
    public string Value(string filePath, int column)
    {
        if (!_values.TryGetValue(filePath, out var cache))
        {
            cache = [];
            _values[filePath] = cache;
        }
        if (cache.TryGetValue(column, out var cached))
        {
            return cached;
        }
        string value;
        if (!PropertyStoreReader.CanOpen(filePath))
        {
            value = UnknownTag;
        }
        else
        {
            try
            {
                dynamic? item = Folder(Path.GetDirectoryName(filePath) ?? "")?.ParseName(Path.GetFileName(filePath));
                value = item is null ? "" : (string?)Folder(Path.GetDirectoryName(filePath) ?? "")!.GetDetailsOf(item, column) ?? "";
            }
            catch (COMException)
            {
                value = "";
            }
        }
        cache[column] = value;
        return value;
    }

    /// <summary>ファイルの値のキャッシュを捨てる（一覧を取り直すとき）。</summary>
    public void ClearValues() => _values.Clear();

    private dynamic? Folder(string path)
    {
        if (path.Length == 0)
        {
            return null;
        }
        if (!_folders.TryGetValue(path, out var folder))
        {
            folder = _shell.NameSpace(path);
            if (folder is not null)
            {
                _folders[path] = folder;
            }
        }
        return folder;
    }
}
