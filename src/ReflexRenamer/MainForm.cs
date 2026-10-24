using ReflexRenamer.Core.Rename;
using ReflexRenamer.Core.Settings;
using ReflexRenamer.Pages;
using ReflexRenamer.Ui;

namespace ReflexRenamer;

/// <summary>
/// メインウィンドウ。上からメニュー、ツールバー（処理単位とフォルダ入力）、ページ切り替えタブ、
/// 左からフォルダツリー・入力フォーム・ファイル一覧、最下段にステータスバーを置く。
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppResources _res;
    private readonly MenuStrip _menu = new();
    private readonly ToolStrip _rebar = new();
    private readonly PageTabStrip _pageTabs;
    private readonly ComboBox _folderBox = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusText = new();
    private readonly ToolStripStatusLabel _displayed = new();
    private readonly ToolStripStatusLabel _selected = new();
    private readonly DockPane _treePane;
    private readonly DockPane _formPane;
    private readonly ShellFolderTree _folderTree;
    private readonly FileListView _list = new();
    private readonly Panel _dock = new() { Dock = DockStyle.Fill, Padding = new Padding(6, 4, 6, 6) };
    private readonly Panel _listPane = new() { Name = "59648" };

    /// <summary>ペインの並びと幅（[BarState-*]）。</summary>
    private BarState _bars = BarState.Default;

    /// <summary>入力フォームの最小幅（リネームページの設計幅＋DockPane の左右の余白）。</summary>
    private int FormPaneMinWidth => DialogBuilder.X(_res.Resources.Dialogs[143].Width) + 6 + 8;
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

        Text = $"{AppResources.ProductName} {AppResources.DisplayVersion}";
        Font = AppResources.UiFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(1084, 721);
        StartPosition = FormStartPosition.WindowsDefaultLocation;
        Icon = Assets.AppIcon() ?? Icon;
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
        // 表示 > 配色（移植版で足した項目。メニューリソースにはないので、フォントの項目があるメニューの末尾に足す）
        if (MenuBuilder.Find(_menu.Items, Commands.Font)?.OwnerItem is ToolStripMenuItem view)
        {
            view.DropDownItems.Add(new ToolStripSeparator());
            view.DropDownItems.Add(BuildColorModeMenu());
        }
        // メニューバーはシステムのメニューフォント（オリジナル版も同じ）
        _menu.Font = SystemFonts.MenuFont ?? Font;

        // ツールバーとページ切り替えタブ
        BuildRebar();
        _pageTabs = new PageTabStrip(res, ExecuteCommand);

        // ステータスバー
        _statusText.Spring = true;
        _statusText.TextAlign = ContentAlignment.MiddleLeft;
        _displayed.AutoSize = true;
        _displayed.TextAlign = ContentAlignment.MiddleLeft;
        _selected.AutoSize = true;
        _selected.TextAlign = ContentAlignment.MiddleLeft;
        _status.Items.AddRange([_statusText, _displayed, _selected]);
        _status.Font = Font;
        _status.SizingGrip = true;
        _status.AutoSize = true;
        _status.Name = "59393";

        // 左：フォルダツリー
        _folderTree = new ShellFolderTree { Dock = DockStyle.Fill, Name = "1000" };
        _folderTree.FolderSelected += path => ChangeFolder(path, fromTree: true);
        _treePane = new DockPane(res.T("Folder List")) { Name = "126" };
        _treePane.Controls.Add(_folderTree);

        // 中央：入力フォーム
        _formPane = new DockPane("ReflexRenamer") { Name = "127", ShowTitle = false };

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

        _dock.Controls.AddRange([_treePane, _formPane, _listPane]);
        _dock.Layout += (_, _) => LayoutPanes();
        foreach (var pane in new[] { _treePane, _formPane })
        {
            pane.WidthRequested += ResizePane;
        }

        Controls.Add(_dock);
        Controls.Add(_status);
        Controls.Add(_pageTabs);
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

    private int MinWidthOf(int id) => id == BarState.InputFormId ? FormPaneMinWidth : BarState.MinWidth;

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
    /// 配置を画面に反映する。浮かせたペインもドッキングしているものとして扱い、Hidden だけ尊重する。
    /// </summary>
    private void ApplyBarState(BarState state)
    {
        foreach (var p in state.Panes)
        {
            if (p.Floating)
            {
                state = state.Dock(p.Id);
            }
        }
        _bars = state;
        foreach (var p in state.Panes)
        {
            PaneOf(p.Id).Visible = !p.Hidden;
        }
        _dock.PerformLayout();
    }

    private void LayoutPanes()
    {
        var area = _dock.DisplayRectangle;
        int x = area.Left;
        foreach (var p in _bars.Panes.Where(p => !p.Hidden))
        {
            int w = _bars.WidthOf(p.Id);
            PaneOf(p.Id).Bounds = new Rectangle(x, area.Top, w, area.Height);
            x += w;
        }
        _listPane.Bounds = new Rectangle(x, area.Top, Math.Max(area.Right - x, 0), area.Height);
    }

    /// <summary>ペインの右端のドラッグ。ファイル一覧が少し残る範囲で幅を変える。</summary>
    private void ResizePane(DockPane pane, int requested)
    {
        int id = IdOf(pane);
        int width = _bars.ClampWidth(id, requested, _dock.DisplayRectangle.Width, MinWidthOf(id), BarState.MinWidth);
        if (width != _bars.WidthOf(id))
        {
            _bars = _bars.WithWidth(id, width);
            _dock.PerformLayout();
            _dock.Update();
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
                MessageBox.Show(this, _res.T("Although the objects were dropped, none were added to a list.\nCheck the \"Target\" and retry dropping."), AppResources.ProductName);
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
        foreach (var c in new Control[] { _list, _folderTree, _formPane, _pageTabs })
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

    /// <summary>表示 > 配色：システムに従う／ライト／ダーク。選ぶと設定に保存し、次回起動時に反映される。</summary>
    private ToolStripMenuItem BuildColorModeMenu()
    {
        var menu = new ToolStripMenuItem(_res.T("Color &mode")) { Name = "ColorMode" };
        var modes = new[]
        {
            (SystemColorMode.System, _res.T("Follow &system")),
            (SystemColorMode.Classic, _res.T("&Light")),
            (SystemColorMode.Dark, _res.T("&Dark")),
        };
        foreach (var (mode, text) in modes)
        {
            var item = new ToolStripMenuItem(text) { Tag = mode };
            item.Click += (_, _) =>
            {
                _res.SaveColorMode(mode);
                MessageBox.Show(this, _res.T("Changing the color mode is applied at the next starts."), AppResources.ProductName);
            };
            menu.DropDownItems.Add(item);
        }
        menu.DropDownOpening += (_, _) =>
        {
            foreach (ToolStripMenuItem item in menu.DropDownItems)
            {
                item.Checked = Equals(item.Tag, _res.ColorMode);
            }
        };
        return menu;
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
                MessageBox.Show(this, _res.T("Changing the language is applied at the next starts."), AppResources.ProductName);
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
        _rebar.GripStyle = ToolStripGripStyle.Hidden;
        _rebar.Renderer = new FlatToolStripRenderer();
        _rebar.ImageScalingSize = new Size(16, 16);
        _rebar.Padding = new Padding(6, 0, 6, 0);
        _rebar.CanOverflow = false;
        _rebar.Name = "59396";

        _rebar.Items.Add(SpecifyButton(Commands.SpecifyFile, _res.T("Specify File"), "Specify File", FluentGlyphs.SpecifyFile));
        _rebar.Items.Add(SpecifyButton(Commands.SpecifyFolder, _res.T("Specify Folder"), "Specify Folder", FluentGlyphs.SpecifyFolder));
        _pageButtons[Commands.SpecifyFolder].Checked = true;

        _rebar.Items.Add(new ToolStripSeparator());
        _rebar.Items.Add(new ToolStripLabel(_res.T("Folder")));

        _folderBox.Name = "1003";
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

        var folderHost = new ToolStripControlHost(_folderBox) { AutoSize = false, Width = 300 };
        _rebar.Items.Add(folderHost);

        void ResizeFolderBox()
        {
            int others = _rebar.Padding.Horizontal + folderHost.Margin.Horizontal;
            foreach (ToolStripItem item in _rebar.Items)
            {
                if (!ReferenceEquals(item, folderHost))
                {
                    others += item.Width + item.Margin.Horizontal;
                }
            }
            int width = Math.Max(_rebar.ClientSize.Width - others - 4, 50);
            if (folderHost.Width != width || _folderBox.Width != width)
            {
                folderHost.Width = width;
                _folderBox.Width = width;
            }
        }

        _rebar.Layout += (_, _) => ResizeFolderBox();
        _rebar.SizeChanged += (_, _) => ResizeFolderBox();
    }

    /// <summary>
    /// ツールバーのトグルボタン（ファイル指定／フォルダ指定）。
    /// オリジナル版のボタンは UI Automation の名前が英語の短い名前（ステータスの説明文の2行目）なので合わせる。
    /// </summary>
    private ToolStripButton SpecifyButton(int command, string text, string accessibleName, char glyph)
    {
        var button = new ToolStripButton(text)
        {
            Image = FluentGlyphs.Render(glyph, 16, SystemColors.ControlText),
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            TextImageRelation = TextImageRelation.ImageBeforeText,
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
        _pageTabs.SelectedCommand = command;
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
        _folderBox.SelectionStart = 0;
        _folderBox.SelectionLength = 0;
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
        _folderBox.Focus();
        _folderBox.SelectionStart = 0;
        _folderBox.SelectionLength = 0;
    }

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
        System.Text.RegularExpressions.Regex.Replace(format, @"%\d*d", value.ToString(System.Globalization.CultureInfo.InvariantCulture));

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
