using System.Globalization;
using ReflexRenamer.Core.Rename;

namespace ReflexRenamer.Ui;

/// <summary>右側のファイル一覧（詳細表示）。列の構成はページごとに変わる。</summary>
public sealed class FileListView : ListView
{
    private readonly ImageList _icons = new() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
    private Font? _boldFont;

    public FileListView()
    {
        View = View.Details;
        FullRowSelect = true;
        HideSelection = false;
        SmallImageList = _icons;
        DoubleBuffered = true;
        // UI Automation で Table ではなく List として見せる（オリジナル版の SysListView32 と同じ）
        AccessibleRole = AccessibleRole.List;
    }

    /// <summary>変更されるセルの強調表示に使う太字フォント（フォント変更時に作り直す）。</summary>
    public Font BoldFont => _boldFont ??= new Font(Font, Font.Style | FontStyle.Bold);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ShellFolderTree.SetWindowTheme(Handle, "Explorer", null);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        var oldBold = _boldFont;
        _boldFont = new Font(Font, Font.Style | FontStyle.Bold);
        if (oldBold is not null)
        {
            foreach (ListViewItem item in Items)
            {
                foreach (ListViewItem.ListViewSubItem sub in item.SubItems)
                {
                    if (ReferenceEquals(sub.Font, oldBold))
                    {
                        sub.Font = _boldFont;
                    }
                }
            }
            oldBold.Dispose();
        }
        base.OnFontChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _boldFont?.Dispose();
            _icons.Dispose();
            _editor.Dispose();
        }
        base.Dispose(disposing);
    }

    private readonly TextBox _editor = new() { Visible = false, BorderStyle = BorderStyle.FixedSingle };
    private ListViewItem? _editing;
    private int _editingSubItem;
    private Func<ListViewItem, string, bool>? _commit;

    /// <summary>「新しい名前」の列をクリックしたとき（項目と列）。直接編集を始めるために使う。</summary>
    public event Action<ListViewItem, int>? SubItemClicked;

    /// <summary>
    /// 項目の列を直接編集する。Enter で確定、Esc で取り消し、↑↓で確定して上下の項目の編集に移る（オリジナル版の動き）。
    /// commit は新しい文字列を受け取り、受け付けたら true。
    /// </summary>
    public void EditSubItem(ListViewItem item, int subItem, Func<ListViewItem, string, bool> commit)
    {
        EndEdit(apply: true);
        if (!Controls.Contains(_editor))
        {
            Controls.Add(_editor);
            _editor.KeyDown += EditorKeyDown;
            _editor.LostFocus += (_, _) => EndEdit(apply: true);
        }
        _editing = item;
        _editingSubItem = subItem;
        _commit = commit;
        item.EnsureVisible();
        var bounds = item.SubItems[subItem].Bounds;
        if (subItem == 0)
        {
            bounds.Width = Columns[0].Width;
        }
        _editor.Bounds = new Rectangle(bounds.X, bounds.Y - 1, Math.Max(bounds.Width, 40), bounds.Height + 2);
        _editor.Font = Font;
        _editor.Text = item.SubItems[subItem].Text;
        _editor.Visible = true;
        _editor.BringToFront();
        _editor.Focus();
        _editor.SelectAll();
    }

    private void EditorKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                e.SuppressKeyPress = true;
                EndEdit(apply: true);
                break;
            case Keys.Escape:
                e.SuppressKeyPress = true;
                EndEdit(apply: false);
                break;
            case Keys.Up or Keys.Down:
            {
                e.SuppressKeyPress = true;
                var item = _editing;
                int subItem = _editingSubItem;
                var commit = _commit;
                EndEdit(apply: true);
                if (item is not null && commit is not null)
                {
                    int next = item.Index + (e.KeyCode == Keys.Up ? -1 : 1);
                    if (next >= 0 && next < Items.Count)
                    {
                        Items[next].Selected = true;
                        Items[next].Focused = true;
                        EditSubItem(Items[next], subItem, commit);
                    }
                }
                break;
            }
        }
    }

    private void EndEdit(bool apply)
    {
        if (_editing is null)
        {
            return;
        }
        var item = _editing;
        var commit = _commit;
        _editing = null;
        _commit = null;
        _editor.Visible = false;
        if (apply && commit is not null)
        {
            commit(item, _editor.Text);
        }
        Focus();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && HitTest(e.Location) is { Item: { } item, SubItem: { } sub })
        {
            SubItemClicked?.Invoke(item, item.SubItems.IndexOf(sub));
        }
    }

    /// <summary>見出しを右クリックしたとき（画面座標）。グループ表示やシャッフルのメニューを出す。</summary>
    public event Action<Point>? HeaderContextMenu;

    protected override void WndProc(ref Message m)
    {
        const int wmContextMenu = 0x007B;
        const int lvmGetHeader = 0x1000 + 31;
        if (m.Msg == wmContextMenu && IsHandleCreated && m.WParam == SendMessage(Handle, lvmGetHeader, 0, 0))
        {
            int x = unchecked((short)(long)m.LParam);
            int y = unchecked((short)((long)m.LParam >> 16));
            HeaderContextMenu?.Invoke(new Point(x, y));
            return;
        }
        base.WndProc(ref m);
    }

    [System.Runtime.InteropServices.DllImport("user32")]
    private static extern nint SendMessage(nint hwnd, int msg, nint wParam, nint lParam);

    public void SetColumns(params (string Text, int Width, HorizontalAlignment Align)[] columns)
    {
        BeginUpdate();
        Items.Clear();
        Columns.Clear();
        foreach (var (text, width, align) in columns)
        {
            Columns.Add(text, width, align);
        }
        EndUpdate();
    }

    /// <summary>一覧の項目を保ったまま列幅だけを更新する。</summary>
    public void SetColumnWidths(params int[] widths)
    {
        if (Columns.Count != widths.Length)
        {
            return;
        }
        BeginUpdate();
        for (int i = 0; i < widths.Length; i++)
        {
            Columns[i].Width = widths[i];
        }
        EndUpdate();
    }

    /// <summary>日時列の既定幅（現在日時の表示幅 + 余白 16px）。</summary>
    public int MeasureTimeColumnWidth() => TextRenderer.MeasureText(FormatTime(DateTime.Now), Font).Width + 16;

    /// <summary>
    /// 列幅の配分で足りない分（<paramref name="deficit"/>）を、この列を <paramref name="minimum"/> まで縮めて補う。
    /// 同じ幅の列が <paramref name="count"/> 本あるときは、まとめて縮める。
    /// </summary>
    public static void Shrink(ref int width, int minimum, ref int deficit, int count = 1)
    {
        if (deficit <= 0 || width <= minimum)
        {
            return;
        }
        int each = Math.Min(width - minimum, (deficit + count - 1) / count);
        width -= each;
        deficit -= each * count;
    }

    /// <summary>まだ存在しないファイルやフォルダのアイコン（連番オブジェクトのプレビュー）。</summary>
    public void EnsureGenericIcons()
    {
        string temp = Path.GetTempPath();
        if (!_icons.Images.ContainsKey("<dir>") && ShellInfo.SmallIcon(temp) is { } dir)
        {
            _icons.Images.Add("<dir>", dir);
        }
        if (!_icons.Images.ContainsKey("<new>") && ShellInfo.SmallIconForType(".") is { } file)
        {
            _icons.Images.Add("<new>", file);
        }
    }

    /// <summary>ファイルのアイコンを一覧用に登録してキーを返す。拡張子ごとに共有し、exe などは個別にする。</summary>
    public string IconKey(FileItem item)
    {
        string ext = item.IsDirectory ? "<dir>" : Path.GetExtension(item.Name).ToLowerInvariant();
        string key = ext is ".exe" or ".lnk" or ".ico" or ".url" ? item.FullPath : ext;
        if (!_icons.Images.ContainsKey(key) && ShellInfo.SmallIcon(item.FullPath) is { } icon)
        {
            _icons.Images.Add(key, icon);
        }
        return key;
    }

    public void SelectAllItems()
    {
        BeginUpdate();
        foreach (ListViewItem i in Items)
        {
            i.Selected = true;
        }
        EndUpdate();
    }

    public void InvertSelection()
    {
        BeginUpdate();
        foreach (ListViewItem i in Items)
        {
            i.Selected = !i.Selected;
        }
        EndUpdate();
    }

    public void RemoveSelected()
    {
        BeginUpdate();
        foreach (ListViewItem i in SelectedItems.Cast<ListViewItem>().ToList())
        {
            Items.Remove(i);
        }
        EndUpdate();
    }

    /// <summary>「1,024 B」の形式。</summary>
    public static string FormatSize(long size) => size.ToString("N0", CultureInfo.CurrentCulture) + " B";

    /// <summary>「2011/03/06 (日) 8:30:00」の形式（短い日付＋長い時刻）。</summary>
    public static string FormatTime(DateTime t)
    {
        var f = CultureInfo.CurrentCulture.DateTimeFormat;
        return t.ToString(f.ShortDatePattern + " " + f.LongTimePattern, CultureInfo.CurrentCulture);
    }
}
