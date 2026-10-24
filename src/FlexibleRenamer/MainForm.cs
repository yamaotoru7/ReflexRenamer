using FlexibleRenamer.Core.Rename;
using FlexibleRenamer.Core.Settings;
using FlexibleRenamer.Pages;
using FlexibleRenamer.Ui;

namespace FlexibleRenamer;

/// <summary>
/// メインウィンドウ。オリジナル版と同じく、上からメニュー、ツールバー2段（処理単位とフォルダ入力、機能の切り替え）、
/// 左からフォルダツリー・入力フォーム・ファイル一覧、最下段にステータスバーを置く。
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppResources _res;
    private readonly MenuStrip _menu = new();
    private readonly Panel _rebar = new();
    private readonly ToolStrip _specifyBar = new();
    private readonly ToolStrip _pageBar = new();
    private readonly ComboBox _folderBox = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusText = new();
    private readonly ToolStripStatusLabel _displayed = new();
    private readonly ToolStripStatusLabel _selected = new();
    private readonly DockPane _treePane;
    private readonly DockPane _formPane;
    private readonly ShellFolderTree _folderTree;
    private readonly FileListView _list = new();
    private readonly Panel _dock = new() { Dock = DockStyle.Fill };
    private readonly Panel _listPane = new() { Name = "59648", Padding = new Padding(2) };

    /// <summary>ペインの並びと幅（[BarState-*]）。</summary>
    private BarState _bars = BarState.Default;

    /// <summary>浮かせたペインのウィンドウ（一度浮かせたら作っておく）。</summary>
    private readonly Dictionary<int, FloatingPaneWindow> _floatWindows = [];

    /// <summary>ApplyBarState 中（ウィンドウの移動で _bars を書き換えない）。</summary>
    private bool _applyingBars;

    /// <summary>入力フォームの最小幅。オリジナル版は ini の sizeVertCX が 262 でも 271px で表示するので、フォームの幅より狭くならない。</summary>
    private const int FormPaneMinWidth = 271;
    private readonly Dictionary<int, ToolStripButton> _pageButtons = [];
    private readonly Dictionary<int, PageBase> _pages = [];
    private PageBase? _page;
    private string _folder;

    /// <param name="initialPage">起動時に開くページ（「送る」のショートカットの引数 /Rename などから。null なら前回のページ）。</param>
    public MainForm(AppResources res, string? initialFolder, int? initialPage = null)
    {
        _res = res;
        var settings = res.Settings;
        // 起動時のフォルダ：引数 > 前回のフォルダ > カレント
        string? remembered = settings.Get("Flexible Renamer", "CurrentFolder");
        _folder = initialFolder is not null && Directory.Exists(initialFolder) ? Path.GetFullPath(initialFolder)
            : remembered is not null && Directory.Exists(remembered) ? remembered
            : Environment.CurrentDirectory;

        Text = "Flexible Renamer v8.4";
        Font = AppResources.UiFont;
        ClientSize = new Size(1084, 721);
        StartPosition = FormStartPosition.WindowsDefaultLocation;
        Icon = Assets.Icon(64) ?? Icon; // オリジナル版のメインフレームの ID（IDR_MAINFRAME）は 64
        KeyPreview = true;

        // メニュー
        _menu.Items.AddRange(MenuBuilder.Build(res.Resources.Menus[Commands.MainMenu], res, ExecuteCommand));
        // お気に入りと言語のメニューは、開くたびに中身を作り直す
        if (MenuBuilder.Find(_menu.Items, Commands.FirstFavorite)?.OwnerItem is ToolStripMenuItem favorites)
        {
            favorites.DropDownItems.Remove(MenuBuilder.Find(_menu.Items, Commands.FirstFavorite)!);
            favorites.DropDownOpening += (_, _) => RebuildFavoritesMenu(favorites);
        }
        if (MenuBuilder.Find(_menu.Items, Commands.Language)?.OwnerItem is ToolStripMenuItem language)
        {
            language.DropDownOpening += (_, _) => RebuildLanguageMenu(language);
        }
        // メニューバーはシステムのメニューフォント（オリジナル版も同じ）。高さは Win32 のメニューバーに合わせる
        _menu.Font = SystemFonts.MenuFont ?? Font;
        _menu.AutoSize = false;
        _menu.Height = SystemInformation.MenuHeight;
        _menu.Padding = new Padding(2, 0, 0, 0);

        // ツールバー（リバー）
        BuildRebar();

        // ステータスバー
        _statusText.Spring = true;
        _statusText.TextAlign = ContentAlignment.MiddleLeft;
        // 件数の欄は幅 100 で左寄せ（オリジナル版も収まらない分は右が切れる）
        _displayed.AutoSize = false;
        _displayed.Width = 100;
        _displayed.TextAlign = ContentAlignment.MiddleLeft;
        _selected.AutoSize = false;
        _selected.Width = 100;
        _selected.TextAlign = ContentAlignment.MiddleLeft;
        _status.Items.AddRange([_statusText, _displayed, _selected]);
        _status.Font = Font;
        _status.SizingGrip = true;
        _status.AutoSize = false;
        _status.Height = 19;
        _status.Name = "59393";

        // 左：フォルダツリー
        _folderTree = new ShellFolderTree { Dock = DockStyle.Fill, Name = "1000" };
        _folderTree.FolderSelected += path => ChangeFolder(path, fromTree: true);
        _treePane = new DockPane(res.T("Folder List")) { Name = "126" };
        _treePane.Controls.Add(_folderTree);

        // 中央：入力フォーム
        _formPane = new DockPane("FlexibleRenamer") { Name = "127", ShowTitle = false };

        // 右：ファイル一覧（2px の枠の中）
        _list.Dock = DockStyle.Fill;
        _list.Name = "1000";
        _list.SelectedIndexChanged += (_, _) => UpdateCounts();
        _list.ColumnClick += (_, e) => _page?.SortBy(e.Column);
        _list.HeaderContextMenu += ShowHeaderMenu;
        // 「新しい名前」の列をクリックすると直接編集、ダブルクリックで関連付けられたアプリで開く
        _list.SubItemClicked += (item, column) =>
        {
            if (column == 1 && _page is RenamePageBase rp && item.Selected)
            {
                rp.EditName(item);
            }
        };
        _list.DoubleClick += (_, _) => OpenSelectedItem();
        _list.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && _list.HitTest(e.Location).Item is { } hit && SelectedPath(hit) is { } path)
            {
                ShellContextMenu.Show(this, _res, path, _list.PointToScreen(e.Location));
            }
        };

        // ドラッグ＆ドロップ：フォルダ指定ではフォルダを開き、ファイル指定では一覧に加える
        AllowDrop = true;
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => DropPaths((string[]?)e.Data?.GetData(DataFormats.FileDrop) ?? []);
        foreach (var c in new Control[] { _list, _folderTree, _formPane })
        {
            c.AllowDrop = true;
            c.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
            c.DragDrop += (_, e) => DropPaths((string[]?)e.Data?.GetData(DataFormats.FileDrop) ?? []);
        }

        // 表示中のフォルダの監視（変化があれば少し待ってから一覧を作り直す）
        _watchTimer.Interval = 700;
        _watchTimer.Tick += (_, _) => { _watchTimer.Stop(); if (!_busy && CanFocus) RefreshList(); };
        _watcher.Changed += (_, _) => RestartWatchTimer();
        _watcher.Created += (_, _) => RestartWatchTimer();
        _watcher.Deleted += (_, _) => RestartWatchTimer();
        _watcher.Renamed += (_, _) => RestartWatchTimer();
        _listPane.Controls.Add(_list);

        // ペインの横位置はオリジナル版の実測に合わせる（左のペインはウィンドウの枠に 2px 食い込み、隣どうしは 2px 重なる）
        _dock.Controls.AddRange([_treePane, _formPane, _listPane]);
        _dock.Layout += (_, _) => LayoutPanes();
        foreach (var pane in new[] { _treePane, _formPane })
        {
            pane.WidthRequested += ResizePane;
            pane.MoveRequested += MovePane;
            pane.FloatRequested += p => FloatPane(p, null);
        }

        Controls.Add(_dock);
        Controls.Add(_status);
        Controls.Add(_rebar);
        Controls.Add(_menu);
        MainMenuStrip = _menu;

        _pages[Commands.PageRename] = new RenamePage(res, this);
        _pages[Commands.PageTagRename] = new TagRenamePage(res, this);
        _pages[Commands.PageAttribute] = new AttributePage(res, this);
        _pages[Commands.PageNumberedObject] = new NumberedObjectPage(res, this);
        foreach (var page in _pages.Values)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _formPane.Controls.Add(page);
        }

        Load += (_, _) =>
        {
            RestoreWindowPlacement();
            RestoreBarState();
            if (SavedFont() is { } font)
            {
                ApplyFont(font);
            }
            foreach (var page in _pages.Values)
            {
                page.LoadSettings();
            }
            int pageCommand = initialPage ?? Commands.PageRename + Math.Clamp(settings.GetInt("Flexible Renamer", "Page", 1) - 1, 0, 3);
            ShowPage(_pages.ContainsKey(pageCommand) ? pageCommand : Commands.PageRename);
            ChangeFolder(_folder, fromTree: false);
        };
        FormClosing += (_, _) => SaveSettings();
    }

    /// <summary>
    /// ウィンドウ位置。オリジナル版（MFC の WINDOWPLACEMENT）と同じ
    /// 「flags:showCmd:minX:minY:maxX:maxY:left:top:right:bottom」の形式で [MainFrame] WindowPos に持つ。
    /// </summary>
    private void RestoreWindowPlacement()
    {
        var parts = (_res.Settings.Get("MainFrame", "WindowPos") ?? "").Split(':');
        if (parts.Length != 10 || !parts.All(p => int.TryParse(p, out _)))
        {
            return;
        }
        var v = parts.Select(int.Parse).ToArray();
        var rect = Rectangle.FromLTRB(v[6], v[7], v[8], v[9]);
        if (rect.Width > 100 && rect.Height > 100 && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect)))
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = rect;
        }
        if (v[1] == 3)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private DockPane PaneOf(int id) => id == BarState.FolderTreeId ? _treePane : _formPane;

    private static int IdOf(DockPane pane) => int.Parse(pane.Name, System.Globalization.CultureInfo.InvariantCulture);

    private static int MinWidthOf(int id) => id == BarState.InputFormId ? FormPaneMinWidth : BarState.MinWidth;

    /// <summary>[BarState-*] からペインの並び・幅・浮かせた状態を戻す（なければ既定の配置）。</summary>
    private void RestoreBarState()
    {
        var state = BarState.Load(_res.Settings) ?? BarState.Default;
        foreach (var pane in state.Panes)
        {
            state = state.WithWidth(pane.Id, Math.Max(pane.Width, MinWidthOf(pane.Id)));
        }
        if (!_res.Settings.GetBool("MainFrame", "ShowShellFolderBar", true))
        {
            state = state.SetHidden(BarState.FolderTreeId, true);
        }
        ApplyBarState(state);
    }

    /// <summary>
    /// 配置を画面に反映する。浮かせたペインはツールウィンドウへ、それ以外は左の列へ入れる。
    /// </summary>
    private void ApplyBarState(BarState state)
    {
        _bars = state;
        _applyingBars = true;
        try
        {
            foreach (var p in state.Panes)
            {
                var pane = PaneOf(p.Id);
                if (p.Floating)
                {
                    var window = FloatWindowOf(pane);
                    var (w, h) = state.FloatSizeOf(p.Id);
                    window.ClientSize = new Size(w, h);
                    window.Location = FloatLocation(p, pane, window);
                    if (pane.Parent != window)
                    {
                        pane.Floating = true;
                        pane.Parent = window;
                        pane.Dock = DockStyle.Fill;
                    }
                    pane.Visible = true;
                    window.Visible = !p.Hidden;
                }
                else
                {
                    if (pane.Parent != _dock)
                    {
                        pane.Dock = DockStyle.None;
                        pane.Floating = false;
                        pane.Parent = _dock;
                    }
                    if (_floatWindows.TryGetValue(p.Id, out var window))
                    {
                        window.Visible = false;
                    }
                    pane.Visible = !p.Hidden;
                }
            }
            // 重なる 2px は左のペインが上になる。ファイル一覧は一番下
            foreach (var pane in state.Panes.Reverse().Where(p => !p.Floating))
            {
                PaneOf(pane.Id).BringToFront();
            }
            _listPane.SendToBack();
        }
        finally
        {
            _applyingBars = false;
        }
        _dock.PerformLayout();
    }

    /// <summary>浮かせたウィンドウの位置。浮かせたことがない、または画面の外なら、ペインのいまの位置に出す。</summary>
    private Point FloatLocation(DockedPane p, DockPane pane, Form window)
    {
        var at = new Point(p.FloatX, p.FloatY);
        if (p.FloatX != DockedPane.Unspecified
            && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(at, window.Size))))
        {
            return at;
        }
        return pane.IsHandleCreated && pane.Parent == _dock ? pane.PointToScreen(Point.Empty) : new Point(Left + 40, Top + 80);
    }

    private FloatingPaneWindow FloatWindowOf(DockPane pane)
    {
        int id = IdOf(pane);
        if (!_floatWindows.TryGetValue(id, out var window))
        {
            window = new FloatingPaneWindow(pane, this);
            window.RedockRequested += w => ApplyBarState(_bars.Dock(IdOf(w.Pane)));
            // 閉じるボタンはペインを隠す（ウィンドウレイアウトのリセットで戻る）
            window.CloseRequested += w => ApplyBarState(_bars.SetHidden(IdOf(w.Pane), true));
            window.LocationChanged += (_, _) => RememberFloat(window);
            window.ClientSizeChanged += (_, _) => RememberFloat(window);
            _floatWindows[id] = window;
        }
        return window;
    }

    /// <summary>浮かせたウィンドウを動かしたり大きさを変えたりしたら覚えておく（ini の MRUFloat*、sizeFloat*）。</summary>
    private void RememberFloat(FloatingPaneWindow window)
    {
        int id = IdOf(window.Pane);
        if (_applyingBars || !window.Visible || window.WindowState != FormWindowState.Normal || _bars.Find(id) is not { Floating: true })
        {
            return;
        }
        _bars = _bars.Float(id, window.Left, window.Top, window.ClientSize.Width, window.ClientSize.Height);
    }

    /// <summary>
    /// ペインを浮かせる。<paramref name="frame"/> はつまみのドラッグで放した枠（スクリーン座標）。
    /// null（つまみのダブルクリック）なら前に浮かせた位置、なければいまの位置に出す。
    /// </summary>
    private void FloatPane(DockPane pane, Rectangle? frame)
    {
        int id = IdOf(pane);
        var p = _bars.Find(id)!;
        var (w, h) = _bars.FloatSizeOf(id);
        var at = frame?.Location ?? (p.FloatX != DockedPane.Unspecified ? new Point(p.FloatX, p.FloatY) : pane.PointToScreen(Point.Empty));
        ApplyBarState(_bars.Float(id, at.X, at.Y, w, h));
    }

    private void LayoutPanes()
    {
        int h = _dock.ClientSize.Height;
        foreach (var (id, left) in _bars.Lefts())
        {
            PaneOf(id).Bounds = new Rectangle(left, 0, _bars.WidthOf(id), h);
        }
        int contentLeft = _bars.ContentLeft();
        _listPane.Bounds = new Rectangle(contentLeft, 0, Math.Max(_dock.ClientSize.Width - contentLeft, 0), h);
    }

    /// <summary>ペインの右端のドラッグ。ファイル一覧が少し残る範囲で幅を変える。</summary>
    private void ResizePane(DockPane pane, int requested)
    {
        int id = IdOf(pane);
        int width = _bars.ClampWidth(id, requested, _dock.ClientSize.Width, MinWidthOf(id), BarState.MinWidth);
        if (width != _bars.WidthOf(id))
        {
            _bars = _bars.WithWidth(id, width);
            _dock.PerformLayout();
            _dock.Update();
        }
    }

    /// <summary>
    /// つまみのドラッグ。左のペインの列の中で放したら、放した位置より左にある（中心が左の）ペインの次へ移す。
    /// 列の外（ファイル一覧の上やウィンドウの外）で放したら、描いていた枠の位置に浮かせる。
    /// </summary>
    private void MovePane(DockPane pane, Point screen, Rectangle frame)
    {
        int id = IdOf(pane);
        var at = _dock.PointToClient(screen);
        var column = new Rectangle(0, 0, Math.Max(_bars.ContentLeft(), 0), _dock.ClientSize.Height);
        if (!column.Contains(at))
        {
            FloatPane(pane, frame);
            return;
        }
        var others = _bars.Panes.Where(p => p.Id != id).ToList();
        int index = others.FindIndex(p => p.InDock && PaneOf(p.Id).Bounds is var b && at.X < b.Left + b.Width / 2);
        var moved = _bars.MoveTo(id, index < 0 ? others.Count : index);
        if (!moved.Panes.Select(p => p.Id).SequenceEqual(_bars.Panes.Select(p => p.Id)))
        {
            ApplyBarState(moved);
        }
    }

    private void SaveSettings()
    {
        var settings = _res.Settings;
        var rect = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        int showCmd = WindowState == FormWindowState.Maximized ? 3 : 1;
        settings.Set("MainFrame", "WindowPos", $"0:{showCmd}:-1:-1:-1:-1:{rect.Left}:{rect.Top}:{rect.Right}:{rect.Bottom}");
        settings.Set("MainFrame", "ShowShellFolderBar", _bars.Find(BarState.FolderTreeId) is not { Hidden: true });
        var screen = SystemInformation.VirtualScreen;
        _bars.Save(settings, _dock.ClientSize.Height, screen.Width, screen.Height);
        settings.Set("Flexible Renamer", "CurrentFolder", _folder);
        int pageIndex = _pages.FirstOrDefault(p => p.Value == _page).Key is var cmd && cmd != 0 ? cmd - Commands.PageRename + 1 : 1;
        settings.Set("Flexible Renamer", "Page", pageIndex);
        settings.Set("Flexible Renamer", "Specify", _pageButtons[Commands.SpecifyFolder].Checked ? 1 : 0);
        settings.Set("Flexible Renamer", "FontFace", Font.Name);
        foreach (var page in _pages.Values)
        {
            page.SaveSettings();
        }
        try
        {
            settings.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public FileListView List => _list;

    public string CurrentFolder => _folder;

    private readonly FileSystemWatcher _watcher = new() { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size };
    private readonly System.Windows.Forms.Timer _watchTimer = new();
    private bool _busy;
    private readonly List<string> _specifiedPaths = [];

    /// <summary>「ファイル指定」モード（ドロップしたファイルだけを一覧にする）。</summary>
    public bool FileMode => _pageButtons[Commands.SpecifyFile].Checked;

    public IReadOnlyList<string> SpecifiedPaths => _specifiedPaths;

    private void RestartWatchTimer()
    {
        if (IsHandleCreated)
        {
            BeginInvoke(() => { _watchTimer.Stop(); _watchTimer.Start(); });
        }
    }

    private void WatchFolder(string folder)
    {
        try
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Path = folder;
            _watcher.EnableRaisingEvents = !FileMode;
        }
        catch (Exception e) when (e is ArgumentException or IOException)
        {
        }
    }

    private void DropPaths(string[] paths)
    {
        if (paths.Length == 0)
        {
            return;
        }
        if (FileMode)
        {
            foreach (var p in paths)
            {
                if (!_specifiedPaths.Contains(p, StringComparer.OrdinalIgnoreCase))
                {
                    _specifiedPaths.Add(p);
                }
            }
            RefreshList();
            if (_list.Items.Count == 0)
            {
                MessageBox.Show(this, _res.T("Although the objects were dropped, none were added to a list.\nCheck the \"Target\" and retry dropping."), "Flexible Renamer");
            }
        }
        else
        {
            string folder = Directory.Exists(paths[0]) ? paths[0] : Path.GetDirectoryName(paths[0]) ?? paths[0];
            ChangeFolder(folder, fromTree: false);
        }
    }

    private void SetFileMode(bool fileMode)
    {
        _pageButtons[Commands.SpecifyFile].Checked = fileMode;
        _pageButtons[Commands.SpecifyFolder].Checked = !fileMode;
        _folderBox.Enabled = !fileMode;
        if (!fileMode)
        {
            _specifiedPaths.Clear();
        }
        WatchFolder(_folder);
        RefreshList();
    }

    private static string? SelectedPath(ListViewItem item) => item.Tag switch
    {
        PreviewRow row => row.Item.FullPath,
        FileItem file => file.FullPath,
        _ => null,
    };

    private void OpenSelectedItem()
    {
        if (_list.SelectedItems.Count == 0 || SelectedPath(_list.SelectedItems[0]) is not { } path)
        {
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    /// <summary>フォント設定（表示 > フォント）。ページ、一覧、ツリーに適用し、設定に保存する。</summary>
    private void ChooseFont()
    {
        using var dialog = new FontDialog { Font = _list.Font, ShowEffects = false };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        ApplyFont(dialog.Font);
    }

    private void ApplyFont(Font font)
    {
        AppResources.UiFont = font;
        _res.Builder.Font = font;
        foreach (var c in new Control[] { _list, _folderTree, _formPane })
        {
            SetFontRecursive(c, font);
        }
        var settings = _res.Settings;
        settings.Set("Flexible Renamer", "FontFace", font.Name);
        int heightPx = -(int)Math.Round(font.SizeInPoints * 96 / 72);
        settings.Set("Flexible Renamer", "Font", $"{heightPx},0,0,0,{(font.Bold ? 700 : 400)},{(font.Italic ? 1 : 0)},0,0,0,0,0,0,34");
    }

    private static void SetFontRecursive(Control control, Font font)
    {
        control.Font = font;
        foreach (Control child in control.Controls)
        {
            SetFontRecursive(child, font);
        }
    }

    /// <summary>設定の Font（LOGFONT の並び）と FontFace からフォントを作る。</summary>
    private Font? SavedFont()
    {
        string? face = _res.Settings.Get("Flexible Renamer", "FontFace");
        var parts = (_res.Settings.Get("Flexible Renamer", "Font") ?? "").Split(',');
        if (face is null || parts.Length < 6 || !int.TryParse(parts[0], out int height) || height == 0)
        {
            return null;
        }
        float points = Math.Abs(height) * 72f / 96f;
        var style = FontStyle.Regular;
        if (int.TryParse(parts[4], out int weight) && weight >= 600) style |= FontStyle.Bold;
        if (parts[5] == "1") style |= FontStyle.Italic;
        try
        {
            return new Font(face, points, style);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private const string FavoritesSection = "Favorites";

    /// <summary>お気に入りメニューを開くたびに、設定の一覧で項目を作り直す。</summary>
    private void RebuildFavoritesMenu(ToolStripMenuItem menu)
    {
        var keep = menu.DropDownItems.Cast<ToolStripItem>().Take(3).ToList(); // 追加、整理、区切り
        menu.DropDownItems.Clear();
        menu.DropDownItems.AddRange(keep.ToArray());
        foreach (var folder in _res.Settings.GetMru(FavoritesSection))
        {
            var item = new ToolStripMenuItem(folder);
            item.Click += (_, _) => ChangeFolder(folder, fromTree: false);
            menu.DropDownItems.Add(item);
        }
    }

    private void OrganizeFavorites()
    {
        using var dialog = new Dialogs.OrderPresetDialog(_res, _res.T("Order Favorites"),
            _res.Settings.GetMru(FavoritesSection).Select(f => (f, "", (object)f)), canRename: false);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _res.Settings.SetList(FavoritesSection, "MRU", dialog.Result.Select(r => (string)r.Tag));
        }
    }

    /// <summary>ヘルプ > 言語：言語ファイルの一覧。選ぶと設定に保存し、次回起動時に反映される。</summary>
    private void RebuildLanguageMenu(ToolStripMenuItem menu)
    {
        menu.DropDownItems.Clear();
        string current = _res.Strings.ShortName;
        foreach (var (code, name) in _res.AvailableLanguages())
        {
            var item = new ToolStripMenuItem(name) { Checked = string.Equals(code, current, StringComparison.OrdinalIgnoreCase) };
            item.Click += (_, _) =>
            {
                _res.Settings.Set("Flexible Renamer", "Language", code);
                MessageBox.Show(this, _res.T("Changing the language is applied at the next starts."), "Flexible Renamer");
            };
            menu.DropDownItems.Add(item);
        }
    }

    private void OpenHelp()
    {
        string chm = Path.Combine(_res.BaseDirectory, $"Flexible Renamer.{_res.Strings.ShortName}.chm");
        if (!File.Exists(chm))
        {
            chm = Path.Combine(_res.BaseDirectory, "Flexible Renamer.EN.chm");
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("hh.exe", $"\"{chm}\"") { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    private void OpenOnline()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_res.T("http://hp.vector.co.jp/authors/VA014830/english/FlexRena/")) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    public void SetStatus(string text) => _statusText.Text = text;

    private void BuildRebar()
    {
        _rebar.Dock = DockStyle.Top;
        _rebar.Height = 68;
        _rebar.Name = "59396";

        // ボタンはツールバーの左端から並ぶ（オリジナル版の実測）
        _specifyBar.GripStyle = ToolStripGripStyle.Hidden;
        _specifyBar.Padding = Padding.Empty;
        _specifyBar.CanOverflow = false;
        _specifyBar.Dock = DockStyle.None;
        _specifyBar.AutoSize = false;
        _specifyBar.Bounds = new Rectangle(2, 2, 190, 32);
        _specifyBar.Name = "59392";
        _specifyBar.ImageScalingSize = new Size(24, 24);
        _specifyBar.Items.Add(PageButton(Commands.SpecifyFile, res => res.T("Specify File"), "Specify File"));
        _specifyBar.Items.Add(PageButton(Commands.SpecifyFolder, res => res.T("Specify Folder"), "Specify Folder"));
        _pageButtons[Commands.SpecifyFolder].Checked = true;

        var folderLabel = new Win32Label { Text = _res.T("Folder"), AutoSize = false, FlatStyle = FlatStyle.System, StaticStyle = 0x200, Bounds = new Rectangle(199, 5, 43, 22) };
        // ラベルはコンボボックス（左端 243）に重ねない（重ねると先頭の文字が隠れる）。余白のない Win32 の STATIC（SS_CENTERIMAGE）で縦中央に置く
        _folderBox.Name = "1003";
        _folderBox.Bounds = new Rectangle(243, 5, 841, 22);
        _folderBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ChangeFolder(_folderBox.Text, fromTree: false);
            }
        };
        // ドロップダウンから履歴を選んだら移動する
        _folderBox.SelectionChangeCommitted += (_, _) => BeginInvoke(() => ChangeFolder(_folderBox.Text, fromTree: false));
        RememberFolder(_folder);

        // ボタンはツールバーの左端から並ぶ（オリジナル版の実測）
        _pageBar.GripStyle = ToolStripGripStyle.Hidden;
        _pageBar.Padding = Padding.Empty;
        _pageBar.CanOverflow = false;
        _pageBar.Dock = DockStyle.None;
        _pageBar.AutoSize = false;
        _pageBar.Bounds = new Rectangle(2, 36, 1082, 32);
        _pageBar.Name = "59392";
        _pageBar.ImageScalingSize = new Size(24, 24);
        _pageBar.Items.Add(PageButton(Commands.PageRename, res => res.T("Rename"), "Rename"));
        _pageBar.Items.Add(PageButton(Commands.PageTagRename, res => res.T("TagRename"), "TagRename"));
        _pageBar.Items.Add(PageButton(Commands.PageAttribute, res => res.T("Attribute"), "Modify attributes"));
        _pageBar.Items.Add(PageButton(Commands.PageNumberedObject, res => res.T("NumberedObject"), "Create numbered object"));
        _pageBar.Items.Add(new ToolStripSeparator());

        _rebar.Controls.AddRange([_specifyBar, folderLabel, _folderBox, _pageBar]);
        // 右端まで伸びる部品はリバーの幅から直接決める（Anchor だと Dock 前の大きさを基準にしてしまう）
        _rebar.Layout += (_, _) =>
        {
            int right = _rebar.ClientSize.Width;
            _folderBox.Width = Math.Max(right - _folderBox.Left, 50);
            _pageBar.Width = Math.Max(right - _pageBar.Left, 50);
        };
    }

    /// <summary>
    /// ツールバーのボタン。オリジナル版のボタンは UI Automation の名前が英語の短い名前（ステータスの説明文の2行目）なので合わせる。
    /// </summary>
    private ToolStripButton PageButton(int command, Func<AppResources, string> text, string accessibleName)
    {
        var button = new ToolStripButton(text(_res))
        {
            Image = Assets.Icon(command)?.ToBitmap(),
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            AutoSize = false,
            Size = command is Commands.SpecifyFile or Commands.SpecifyFolder ? new Size(94, 30) : new Size(110, 30),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = accessibleName,
            AccessibleRole = AccessibleRole.CheckButton,
        };
        button.Click += (_, _) => ExecuteCommand(command);
        _pageButtons[command] = button;
        return button;
    }

    private const int MenuListHeader = 181;
    private const int CmdGroupNone = 32955;
    private const int CmdGroupFolder = 32956;
    private const int CmdGroupDate = 32957;
    private const int CmdGroupType = 32958;
    private const int CmdGroupExtension = 32959;
    private const int CmdShuffle = 32962;

    /// <summary>一覧の見出しを右クリックしたときのメニュー（MENU 181）：グループで表示、シャッフル。</summary>
    private void ShowHeaderMenu(Point screen)
    {
        if (_page is null || !_page.CanArrange)
        {
            return;
        }
        var popup = new PopupMenu(_res);
        popup.Load(MenuListHeader, 0, item =>
        {
            switch (item.Id)
            {
                case CmdGroupNone: _page.GroupBy(ListGroup.None); break;
                case CmdGroupFolder: _page.GroupBy(ListGroup.Folder); break;
                case CmdGroupDate: _page.GroupBy(ListGroup.Date); break;
                case CmdGroupType: _page.GroupBy(ListGroup.Type); break;
                case CmdGroupExtension: _page.GroupBy(ListGroup.Extension); break;
                case CmdShuffle: _page.Shuffle(_list.Items.Count); break;
            }
        });
        _page.AddHeaderMenuItems(popup);
        popup.SetChecked(_page.Group switch
        {
            ListGroup.Folder => CmdGroupFolder,
            ListGroup.Date => CmdGroupDate,
            ListGroup.Type => CmdGroupType,
            ListGroup.Extension => CmdGroupExtension,
            _ => CmdGroupNone,
        }, true);
        popup.ShowAt(this, screen);
    }

    /// <summary>メニュー・ツールバー・ショートカットキーから呼ばれるコマンドの処理。</summary>
    public void ExecuteCommand(int command)
    {
        switch (command)
        {
            case Commands.Exit:
                Close();
                break;
            case Commands.PageRename:
            case Commands.PageTagRename:
            case Commands.PageAttribute:
            case Commands.PageNumberedObject:
                ShowPage(command);
                break;
            case Commands.SelectAll:
                _list.SelectAllItems();
                break;
            case Commands.InvertSelection:
                _list.InvertSelection();
                break;
            case Commands.DeleteFromList:
                _list.RemoveSelected();
                UpdateCounts();
                break;
            case Commands.RefreshFolderTree:
                _folderTree.RefreshTree();
                RefreshList();
                break;
            case Commands.ToggleStatusBar:
                _status.Visible = !_status.Visible;
                break;
            case Commands.ToggleToolBar:
                _rebar.Visible = !_rebar.Visible;
                break;
            case Commands.About:
                using (var about = new AboutDialog(_res))
                {
                    about.ShowDialog(this);
                }
                break;
            case Commands.EditName:
                (_page as RenamePageBase)?.EditSelectedName();
                break;
            case Commands.SpecifyFile:
                SetFileMode(true);
                break;
            case Commands.SpecifyFolder:
                SetFileMode(false);
                break;
            case Commands.Font:
                ChooseFont();
                break;
            case Commands.ResetLayout:
                WindowState = FormWindowState.Normal;
                ClientSize = new Size(1084, 721);
                // 浮かせたペインもドッキングに戻し、隠したペインも出す
                ApplyBarState(BarState.Default);
                _rebar.Visible = true;
                _status.Visible = true;
                break;
            case Commands.AddFavorite:
                _res.Settings.PushMru(FavoritesSection, _folder);
                break;
            case Commands.OrganizeFavorites:
                OrganizeFavorites();
                break;
            case Commands.Help:
                OpenHelp();
                break;
            case Commands.Options:
                using (var options = new Dialogs.OptionsDialog(_res))
                {
                    if (options.ShowDialog(this) == DialogResult.OK)
                    {
                        options.Apply();
                    }
                }
                break;
            case Commands.Online:
                OpenOnline();
                break;
            default:
                _page?.ExecuteCommand(command);
                break;
        }
    }

    private void ShowPage(int command)
    {
        if (!_pages.TryGetValue(command, out var page))
        {
            return;
        }
        foreach (var (id, button) in _pageButtons)
        {
            if (id is Commands.PageRename or Commands.PageTagRename or Commands.PageAttribute or Commands.PageNumberedObject)
            {
                button.Checked = id == command;
            }
        }
        _page?.Hide();
        _page = page;
        page.Show();
        page.Activate();
    }

    public void ChangeFolder(string path, bool fromTree)
    {
        if (!Directory.Exists(path))
        {
            return;
        }
        _folder = Path.GetFullPath(path);
        RememberFolder(_folder);
        _folderBox.Text = _folder;
        if (!fromTree)
        {
            _folderTree.SelectPath(_folder);
        }
        WatchFolder(_folder);
        RefreshList();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // オリジナル版は起動直後にフォルダ欄へフォーカスがあり、パスが選択されている
        _folderBox.Focus();
        _folderBox.SelectAll();
        // アクセスキーの下線を常に表示する（オリジナル版はダイアログバーなので Alt を押さなくても下線が出る）
        SendMessage(Handle, 0x0127, (2 << 16) | 2, 0); // WM_CHANGEUISTATE, MAKEWPARAM(UIS_CLEAR, UISF_HIDEACCEL)
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    /// <summary>フォルダ欄の履歴：このセッションで開いたフォルダを新しい順に（オリジナル版と同じく ini には保存しない。explore-folder-history）。</summary>
    private void RememberFolder(string folder)
    {
        for (int i = _folderBox.Items.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_folderBox.Items[i] as string, folder, StringComparison.OrdinalIgnoreCase))
            {
                _folderBox.Items.RemoveAt(i);
            }
        }
        _folderBox.Items.Insert(0, folder);
        while (_folderBox.Items.Count > 32)
        {
            _folderBox.Items.RemoveAt(_folderBox.Items.Count - 1);
        }
    }

    /// <summary>現在のページの条件で一覧を作り直す。</summary>
    public void RefreshList()
    {
        _busy = true;
        SetStatus(string.Format(System.Globalization.CultureInfo.CurrentCulture, _res.T("Searching %s").Replace("%s", "{0}", StringComparison.Ordinal), _folder));
        _page?.RefreshList();
        SetStatus(_res.T("Search completed"));
        UpdateCounts();
        _busy = false;
    }

    public void UpdateCounts()
    {
        _displayed.Text = Printf(_res.T("Displayed: %5d Objects"), _list.Items.Count);
        _selected.Text = Printf(_res.T("Selected: %5d Objects"), _list.SelectedIndices.Count);
    }

    /// <summary>言語ファイルの書式（%5d）に数値を当てはめる。</summary>
    private static string Printf(string format, int value) =>
        System.Text.RegularExpressions.Regex.Replace(format, @"%(\d*)d", m =>
            value.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(m.Groups[1].Length > 0 ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0));

    /// <summary>フォーカスを持つ最も内側のコントロール（ActiveControl はコンテナまでしか返さない）。</summary>
    private Control? FocusedControl()
    {
        Control? c = ActiveControl;
        while (c is ContainerControl container && container.ActiveControl is not null)
        {
            c = container.ActiveControl;
        }
        return c;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // アクセラレータ（ACCELERATOR 64）。入力欄にフォーカスがあるときは編集用のキーを横取りしない
        bool editing = FocusedControl() is TextBoxBase or ComboBox or DateTimePicker;
        int? command = keyData switch
        {
            Keys.Delete when !editing => Commands.DeleteFromList,
            Keys.Control | Keys.I => Commands.InvertSelection,
            Keys.Control | Keys.A when !editing => Commands.SelectAll,
            Keys.Control | Keys.Z when !editing => Commands.Undo,
            Keys.F5 => Commands.RefreshFolderTree,
            Keys.F2 => Commands.EditName,
            _ => null,
        };
        if (command is { } c)
        {
            ExecuteCommand(c);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
