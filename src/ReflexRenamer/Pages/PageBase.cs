using ReflexRenamer.Core.Rename;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Pages;

/// <summary>一覧のグループ表示（見出しの右クリックメニュー）。</summary>
public enum ListGroup
{
    None,
    Folder,
    Date,
    Type,
    Extension,
}

/// <summary>入力フォームのページ（リネーム、タグ・リネーム、属性、連番オブジェクト）の共通部分。</summary>
public abstract class PageBase : UserControl
{
    private int _sortColumn = -1;
    private bool _sortAscending = true;
    private int[]? _shuffle;

    protected PageBase(AppResources res, MainForm main)
    {
        Res = res;
        Main = main;
        Font = AppResources.UiFont;
        AutoScroll = true;
    }

    protected AppResources Res { get; }

    protected MainForm Main { get; }

    public ListGroup Group { get; private set; }

    /// <summary>ページが表示されたとき。一覧の列を切り替えて作り直す。</summary>
    public virtual void Activate() => Main.RefreshList();

    /// <summary>現在のフォルダと条件で一覧を作り直す。</summary>
    public abstract void RefreshList();

    /// <summary>設定ファイルから入力値と履歴を復元する（起動時）。</summary>
    public virtual void LoadSettings()
    {
    }

    /// <summary>入力値と履歴を設定ファイルに書く（終了時。保存自体はメインウィンドウが行う）。</summary>
    public virtual void SaveSettings()
    {
    }

    /// <summary>メインウィンドウで処理しないコマンド。</summary>
    public virtual void ExecuteCommand(int command)
    {
    }

    /// <summary>並べ替えに使う列の値。並べ替えできないページ（連番オブジェクト）は null。</summary>
    protected virtual Func<FileItem, IComparable>? ColumnKey(int column) => null;

    /// <summary>その列を初めてクリックしたときに昇順にするか。</summary>
    protected virtual bool DefaultAscending(int column) => true;

    /// <summary>並べ替えやグループ表示ができるページか。</summary>
    public virtual bool CanArrange => true;

    /// <summary>一覧の見出しの右クリックメニュー（MENU 181）の後ろに、ページ固有の項目を足す（タグ・リネームのタグ列）。</summary>
    public virtual void AddHeaderMenuItems(PopupMenu popup)
    {
    }

    /// <summary>見出しをクリックしたとき：同じ列なら昇順と降順を切り替える。</summary>
    public void SortBy(int column)
    {
        if (ColumnKey(column) is null)
        {
            return;
        }
        // 別の列を初めてクリックしたときの向きは列ごとに決まっている（サイズは降順で、クリックしても反転しない。explore-sort）
        _sortAscending = _sortColumn != column ? DefaultAscending(column) : TogglesDirection(column) ? !_sortAscending : _sortAscending;
        _sortColumn = column;
        _shuffle = null;
        Rearrange();
    }

    public void GroupBy(ListGroup group)
    {
        Group = group;
        Rearrange();
    }

    /// <summary>並べ替え・グループ・シャッフルを、今の一覧の並びに対して適用し直す（ファイルは集め直さない。並べ替えは直前の並びに対して安定）。</summary>
    protected virtual void Rearrange() => RefreshList();

    /// <summary>同じ列をもう一度クリックしたとき向きを反転するか。</summary>
    protected virtual bool TogglesDirection(int column) => true;

    /// <summary>シャッフル：一覧の順番をランダムにする（連番の割り当てが変わる）。</summary>
    public void Shuffle(int count)
    {
        _shuffle = Enumerable.Range(0, count).OrderBy(_ => Random.Shared.Next()).ToArray();
        _sortColumn = -1;
        Rearrange();
    }

    /// <summary>グループのキー（グループ表示とグループごとの連番に使う）。グループ表示でなければ null。</summary>
    protected string? GroupKeyOf(FileItem item) => Group switch
    {
        ListGroup.Folder => item.DirectoryPath,
        ListGroup.Date => item.Modified.Date.ToString("d", System.Globalization.CultureInfo.CurrentCulture),
        ListGroup.Type => ShellInfo.TypeName(item.FullPath, item.IsDirectory),
        ListGroup.Extension => item.IsDirectory ? "" : item.Extension.ToLowerInvariant(),
        _ => null,
    };

    /// <summary>並べ替え・グループ・シャッフルを一覧の並びに反映する。</summary>
    protected List<FileItem> Arrange(List<FileItem> items)
    {
        IEnumerable<FileItem> result = items;
        if (_shuffle is not null && _shuffle.Length == items.Count)
        {
            result = _shuffle.Select(i => items[i]);
        }
        else if (_sortColumn >= 0 && ColumnKey(_sortColumn) is { } key)
        {
            result = _sortAscending ? items.OrderBy(key, ComparableComparer.Instance) : items.OrderByDescending(key, ComparableComparer.Instance);
        }
        if (Group != ListGroup.None)
        {
            // グループ表示では同じグループを続けて並べる。グループの順はキーの昇順（explore-sort：拡張子なしのフォルダと README が先頭、
            // 種類では「ファイル フォルダー」が英字の種類名の後ろ）。グループの中は直前の並びのまま
            result = result.GroupBy(GroupKeyOf).OrderBy(g => g.Key ?? "", NaturalOrder.Instance).SelectMany(g => g);
        }
        return result.ToList();
    }

    /// <summary>
    /// グループ表示の見た目。オリジナル版は見出し行を出さず、グループごとにまとめた並び順で表示するだけ（explore-sort の group-ext）なので、
    /// ListView のグループは使わない。
    /// </summary>
    protected void ApplyGroups(FileListView list, IEnumerable<(ListViewItem Item, FileItem File)> rows)
    {
        _ = rows;
        list.Groups.Clear();
        list.ShowGroups = false;
    }

    /// <summary>文字列は自然順、それ以外は IComparable で比べる。</summary>
    private sealed class ComparableComparer : IComparer<IComparable>
    {
        public static readonly ComparableComparer Instance = new();

        public int Compare(IComparable? x, IComparable? y) => (x, y) switch
        {
            (string a, string b) => NaturalOrder.Instance.Compare(a, b),
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            _ => x.CompareTo(y),
        };
    }
}
