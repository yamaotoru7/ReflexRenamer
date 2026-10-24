using System.Globalization;
using ReflexRenamer.Core.Rename;
using ReflexRenamer.Core.Scripting;
using ReflexRenamer.Core.Settings;
using ReflexRenamer.Dialogs;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Pages;

/// <summary>
/// リネームページとタグ・リネームページの共通部分：連番、実行する動作、リネームの実行、元に戻す、
/// オプション／スクリプトのメニュー、プレビュー一覧の表示。コントロール ID は両方のダイアログ（143、170）で同じ。
/// </summary>
public abstract class RenamePageBase : PageBase
{
    protected const int IdRecursive = 1024;
    protected const int IdDepthLabel = 1053;
    protected const int IdDepth = 1052;
    protected const int IdReplace = 1019;
    protected const int IdStart = 1065;
    protected const int IdStep = 1067;
    protected const int IdResetByFolder = 1173;
    protected const int IdOperation = 1146;
    protected const int IdRename = 1011;
    protected const int IdUndo = 1023;
    protected const int IdPresetButton = 1048;
    protected const int IdNumberingButton = 1054;
    protected const int IdOptionButton = 1049;
    protected const int IdScriptButton = 1047;
    protected const int IdScript = 1026;
    protected const int MenuScript = 182;
    protected const int MenuNumbering = 304;
    protected const int CmdNumberingDialog = 32917;
    protected const int CmdTimestampUpdated = 32885;
    protected const int CmdTimestampCreated = 32886;
    protected const int CmdHideProtected = 32906;
    protected const int CmdEditScript = 32967;
    protected const int CmdAskByMail = 32887;

    private readonly ScriptWorkspace _scripts;
    private ScriptLanguage _scriptLanguage;
    private ComboEditSelection? _scriptSelection;
    protected string SelectedScriptLanguage => _scriptLanguage.ToString();

    protected void SelectScriptLanguage(string name)
    {
        _scriptLanguage = name.Equals("JScript", StringComparison.OrdinalIgnoreCase) ? ScriptLanguage.JScript : ScriptLanguage.VBScript;
    }

    protected RenamePageBase(AppResources res, MainForm main, int dialogId, int pageCommand) : base(res, main)
    {
        _scripts = new ScriptWorkspace(res.BaseDirectory);
        var template = res.Resources.Dialogs[dialogId];
        Size = DialogBuilder.DesignSize(template);
        Map = res.Builder.Build(dialogId, template, this);
        Tooltips.Apply(dialogId, Map, res, new ToolTip());
        // テンプレートにある小さな一覧（1000、1001）は置き場で、オリジナル版では表示されない
        Map[1000].Visible = false;
        Map[1001].Visible = false;
        // 左上のアイコン（ID 1009）はページのアイコン（コマンド ID と同じ番号のアイコンリソース）
        Map.Get<PictureBox>(1009).Image = Assets.Icon(pageCommand, 32)?.ToBitmap();
        Popup = new PopupMenu(res);
    }

    protected ControlMap Map { get; }

    protected PopupMenu Popup { get; }

    protected bool Updating { get; set; }

    protected List<FileItem> Items { get; set; } = [];

    protected List<PreviewRow> Rows { get; private set; } = [];

    /// <summary>直接編集で変えた新しい名前（パス → 名前）。検索・置換が変わるか、一覧を作り直すと消える。</summary>
    private readonly Dictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private (string Search, string Replace) _overrideBasis;

    /// <summary>選択している項目の「新しい名前」を直接編集する（F2）。</summary>
    public void EditSelectedName()
    {
        var list = Main.List;
        if (list.SelectedItems.Count == 0 && list.Items.Count > 0)
        {
            list.Items[0].Selected = true;
        }
        if (list.SelectedItems.Count > 0)
        {
            EditName(list.SelectedItems[0]);
        }
    }

    public void EditName(ListViewItem item) => Main.List.EditSubItem(item, 1, (i, text) =>
    {
        if (i.Tag is not PreviewRow row || text.Length == 0 || text.IndexOfAny(Path.GetInvalidFileNameChars().Where(c => c != '\\' && c != '/').ToArray()) >= 0)
        {
            return false;
        }
        _overrides[row.Item.FullPath] = text;
        UpdatePreview();
        return true;
    });

    /// <summary>ファイル指定モードなら指定されたパスから、そうでなければフォルダから一覧を集める。</summary>
    protected List<FileItem> CollectItems(CollectOptions options) =>
        Main.FileMode ? FileCollector.CollectSpecified(Main.SpecifiedPaths, options) : FileCollector.Collect(Main.CurrentFolder, options);

    protected bool UseCreationTime { get; set; }

    protected bool HideProtected { get; set; } = true;

    protected NumberType NumberType { get; set; } = NumberType.Decimal;

    protected T Get<T>(int id) where T : Control => Map.Get<T>(id);

    /// <summary>現在の入力からリネームの設定を作る。</summary>
    protected abstract RenameOptions CurrentOptions();

    /// <summary>実行したときに入力欄の文字列を履歴に入れる。</summary>
    protected abstract void CommitHistory();

    /// <summary>共通のコントロールの初期値と動作。派生クラスの Initialize から呼ぶ。</summary>
    protected void InitializeCommon()
    {
        // メニューを出すボタン（プリセット・サポート・スクリプト・オプション・連番・正規表現のオプション・タグ一覧）には ▶ を付ける
        foreach (int id in new[] { 1045, 1046, 1047, 1048, 1049, 1051, 1054 })
        {
            if (Map.TryGet(id, out var c) && c is Win32Button button)
            {
                button.MenuArrow = true;
            }
        }
        Get<TextBox>(IdStart).Text = "0";
        Get<TextBox>(IdStep).Text = "1";
        Get<TextBox>(IdDepth).Text = "0";
        var operation = Get<ComboBox>(IdOperation);
        operation.Items.AddRange([
            Res.T("Rename"),
            Res.T("Move&Rename to Another Folder"),
            Res.T("Copy&Rename to Another Folder"),
            Res.T("Create Shortcut&Rename to Another Folder"),
            Res.T("Create HardLink&Rename to Another Folder"),
            Res.T("Move to Recycle Bin"),
        ]);
        operation.SelectedIndex = 0;
        // 実行ボタンの表示名は動作に合わせて変わる（リネーム(R)、移動(M)、コピー(C)、ショートカット(S)、ハードリンク(H)、ごみ箱(R)）
        operation.SelectedIndexChanged += (_, _) =>
        {
            Get<Button>(IdRename).Text = Res.T(operation.SelectedIndex switch
            {
                1 => "&Move",
                2 => "&Copy",
                3 => "&Shortcut",
                4 => "&HardLink",
                5 => "&Recycle Bin",
                _ => "&Rename",
            });
            // 別フォルダへの動作のときだけ ▼ が付き、最近送ったフォルダ（MENU 170）を出せる
            Get<Win32Button>(IdRename).Split = IsToFolder((RenameOperation)operation.SelectedIndex);
        };
        // 実行ボタンはオーナードロー（Button(OwnerDraw)）で、文字は太字
        Get<Button>(IdRename).Font = new Font(Get<Button>(IdRename).Font, FontStyle.Bold);
        Get<Win32Button>(IdRename).DropDown += (_, _) => ShowDestinationMenu();
        Get<Button>(IdUndo).Enabled = false;

        Get<CheckBox>(IdRecursive).CheckedChanged += (_, _) => { UpdateRecursiveEnabled(); Main.RefreshList(); };
        Get<TextBox>(IdDepth).TextChanged += (_, _) => Main.RefreshList();
        foreach (var id in new[] { IdStart, IdStep })
        {
            Get<TextBox>(id).TextChanged += (_, _) => UpdatePreview();
        }
        Get<CheckBox>(IdResetByFolder).CheckedChanged += (_, _) => UpdatePreview();
        Get<CheckBox>(IdScript).CheckedChanged += (_, _) => UpdatePreview();
        Get<ComboBox>(IdReplace).TextChanged += (_, _) => UpdatePreview();
        _scriptSelection = new ComboEditSelection(Get<ComboBox>(IdReplace));
        Get<Button>(IdRename).Click += (_, _) => ExecuteRename(null);
        Get<Button>(IdUndo).Click += (_, _) => Undo();
        Get<Button>(IdNumberingButton).Click += (_, _) => ShowNumberingMenu();
        Get<Button>(IdScriptButton).Click += (_, _) => ShowScriptMenu();
        // スクリプトのボタンは、チェックが付いているときだけ押せる（オリジナル版と同じ）
        Get<CheckBox>(IdScript).CheckedChanged += (_, _) => Get<Button>(IdScriptButton).Enabled = Get<CheckBox>(IdScript).Checked;
        Get<Button>(IdScriptButton).Enabled = Get<CheckBox>(IdScript).Checked;
        Main.Activated += RefreshScriptPreview;
        Main.List.ClientSizeChanged += OnListClientSizeChanged;
        UpdateRecursiveEnabled();
    }

    private void RefreshScriptPreview(object? sender, EventArgs e)
    {
        if (Visible && !Updating && Get<CheckBox>(IdScript).Checked) UpdatePreview();
    }

    protected void UpdateRecursiveEnabled()
    {
        bool recursive = Get<CheckBox>(IdRecursive).Checked;
        Get<TextBox>(IdDepth).Enabled = recursive;
        // オリジナル版は「深さ」のラベル（テンプレートでは無効）を有効にしたまま、色だけ灰色にする
        Get<Label>(IdDepthLabel).Enabled = true;
        Get<Label>(IdDepthLabel).ForeColor = recursive ? SystemColors.ControlText : SystemColors.GrayText;
    }

    protected int Depth => int.TryParse(Get<TextBox>(IdDepth).Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    protected long StartNumber => long.TryParse(Get<TextBox>(IdStart).Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    protected long StepNumber => long.TryParse(Get<TextBox>(IdStep).Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 1;

    protected static void InsertAtCaret(ComboBox combo, string text)
    {
        int start = combo.SelectionStart;
        combo.Text = combo.Text[..start] + text + combo.Text[(start + combo.SelectionLength)..];
        combo.SelectionStart = start + text.Length;
        combo.Focus();
    }

    protected static void AskByMail()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("mailto:naru@yeah.ne.jp?subject=[Flexible Renamer]") { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    private void ShowNumberingMenu()
    {
        Popup.Load(MenuNumbering, 0, item =>
        {
            NumberType = (NumberType)(item.Id - 1);
            UpdatePreview();
        });
        Popup.SetChecked((int)NumberType + 1, true);
        Popup.ShowBelow(Get<Button>(IdNumberingButton));
    }

    /// <summary>オプションボタンのメニュー。ページ固有の項目は <paramref name="extra"/> で処理する（処理したら true）。</summary>
    protected void ShowOptionMenu(int menuId, Func<int, bool>? extra = null)
    {
        Popup.Load(menuId, 0, item =>
        {
            if (extra is not null && extra(item.Id))
            {
                return;
            }
            switch (item.Id)
            {
                case CmdNumberingDialog:
                    using (var dialog = new NumberingDialog(Res, StartNumber, StepNumber, NumberType))
                    {
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            Get<TextBox>(IdStart).Text = dialog.Start.ToString(CultureInfo.InvariantCulture);
                            Get<TextBox>(IdStep).Text = dialog.Step.ToString(CultureInfo.InvariantCulture);
                            NumberType = dialog.Type;
                            UpdatePreview();
                        }
                    }
                    break;
                case CmdTimestampUpdated:
                    UseCreationTime = false;
                    UpdatePreview();
                    break;
                case CmdTimestampCreated:
                    UseCreationTime = true;
                    UpdatePreview();
                    break;
                case CmdHideProtected:
                    HideProtected = !HideProtected;
                    Main.RefreshList();
                    break;
            }
        });
        Popup.SetChecked(UseCreationTime ? CmdTimestampCreated : CmdTimestampUpdated, true);
        Popup.SetChecked(CmdHideProtected, HideProtected);
        Popup.ShowBelow(Get<Button>(IdOptionButton));
    }

    private void ShowScriptMenu()
    {
        Popup.Clear();
        try
        {
            foreach (string name in _scripts.Host(_scriptLanguage).FunctionNames())
            {
                Popup.AddItem(name, () => InsertScript(name + "()"));
            }
        }
        catch (Exception e) when (e is ScriptException or IOException or UnauthorizedAccessException) { }
        Popup.AddSubmenu(Res.T("&Embedded Function"), ScriptBuiltins.For(_scriptLanguage).Select(signature =>
            (signature, (Action)(() => InsertScript(signature)))));
        Popup.AddSubmenu(Res.T("&Script Language"), Enum.GetValues<ScriptLanguage>().Select(language =>
            (language.ToString(), (Action)(() => { _scriptLanguage = language; UpdatePreview(); }), language == _scriptLanguage)));
        foreach (var item in Res.Resources.Menus[MenuScript][0].Children ?? [])
            Popup.AddFromTemplate(item, entry => { if (entry.Id == CmdEditScript) EditScript(); });
        Popup.ShowBelow(Get<Button>(IdScriptButton));
    }

    private void InsertScript(string text)
    {
        var combo = Get<ComboBox>(IdReplace);
        var selection = _scriptSelection!.Selection;
        int start = Math.Min(selection.Start, combo.Text.Length);
        int length = Math.Min(selection.Length, combo.Text.Length - start);
        combo.Focus();
        combo.Select(start, length);
        InsertAtCaret(combo, text);
        int open = text.IndexOf('(');
        if (open >= 0 && text.EndsWith(')')) combo.Select(start + open + 1, text.Length - open - 2);
    }

    private void EditScript()
    {
        try
        {
            string path = _scripts.EnsureUserFile(_scriptLanguage);
            var start = new System.Diagnostics.ProcessStartInfo("notepad.exe") { UseShellExecute = true };
            start.ArgumentList.Add(path);
            System.Diagnostics.Process.Start(start)?.Dispose();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, e.Message, "Flexible Renamer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 付属とユーザーの関数定義を読み込む。保存された変更があればエンジンを作り直す。
    /// </summary>
    protected Func<string, string> ScriptFor(string languageName)
    {
        var language = languageName.Equals("JScript", StringComparison.OrdinalIgnoreCase) ? ScriptLanguage.JScript : ScriptLanguage.VBScript;
        try
        {
            return _scripts.Host(language).Evaluate;
        }
        catch (Exception e) when (e is ScriptException or IOException or UnauthorizedAccessException)
        {
            return _ => throw new ScriptException(e.Message);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Main.Activated -= RefreshScriptPreview;
            Main.List.ClientSizeChanged -= OnListClientSizeChanged;
            _scriptSelection?.Dispose();
            _scripts.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>「新しい名前」の後ろに入れる追加の列（タグ・リネームのタグ列。C12）。</summary>
    protected virtual IReadOnlyList<(string Title, Func<FileItem, string> Value)> ExtraColumns => [];

    public override void Activate()
    {
        ApplyColumns();
        base.Activate();
    }

    private const int BaseColumnCount = 7;
    private const int MinNameColumnWidth = 120;
    private const int FolderColumnWidth = 180;
    private const int SizeColumnWidth = 72;
    private const int TypeColumnWidth = 100;

    /// <summary>ini の ListCtrl-1000-0-0 から読んだ列の幅（7列分。なければ内容に合った既定幅）。</summary>
    private int[]? _savedColumnWidths;
    private int _lastListWidth;

    /// <summary>一覧の列を作り直す。追加の列（タグ列）は見出しの文字の幅に合わせる（オリジナル版の「アーティスト」列は 78px）。</summary>
    protected void ApplyColumns()
    {
        var list = Main.List;
        _lastListWidth = list.ClientSize.Width;
        var extras = ExtraColumns;
        var widths = CalculateColumnWidths(list);
        var columns = new List<(string, int, HorizontalAlignment)>
        {
            (Res.T("Current Name"), widths[0], HorizontalAlignment.Left),
            (Res.T("New Name"), widths[1], HorizontalAlignment.Left),
        };
        for (int i = 0; i < extras.Count; i++)
        {
            columns.Add((extras[i].Title, widths[2 + i], HorizontalAlignment.Left));
        }
        int offset = 2 + extras.Count;
        columns.AddRange(
        [
            (Res.T("Folder"), widths[offset], HorizontalAlignment.Left),
            (Res.T("Size"), widths[offset + 1], HorizontalAlignment.Right),
            (Res.T("Type"), widths[offset + 2], HorizontalAlignment.Left),
            (Res.T("Date Updated"), widths[offset + 3], HorizontalAlignment.Left),
            (Res.T("Date Created"), widths[offset + 4], HorizontalAlignment.Left),
        ]);
        list.SetColumns([.. columns]);
    }

    private int[] CalculateColumnWidths(FileListView list)
    {
        var extraWidths = ExtraColumns.Select(c => TextRenderer.MeasureText(c.Title, list.Font).Width + 19).ToArray();
        if (_savedColumnWidths is { } saved)
        {
            return [saved[0], saved[1], .. extraWidths, saved[2], saved[3], saved[4], saved[5], saved[6]];
        }
        int timeWidth = list.MeasureTimeColumnWidth();
        int folder = FolderColumnWidth;
        int type = TypeColumnWidth;
        // 一覧が狭いときは、名前の 2 列に最低幅を確保するために、情報量の少ない列から縮める（フォルダ → 種類 → 日時）。
        // それでも足りなければ横スクロールになる
        int Fixed() => folder + SizeColumnWidth + type + timeWidth * 2 + extraWidths.Sum();
        int deficit = MinNameColumnWidth * 2 - (list.ClientSize.Width - Fixed());
        FileListView.Shrink(ref folder, 60, ref deficit);
        FileListView.Shrink(ref type, 70, ref deficit);
        FileListView.Shrink(ref timeWidth, 96, ref deficit, count: 2);
        int remaining = Math.Max(list.ClientSize.Width - Fixed(), MinNameColumnWidth * 2);
        int currentNameWidth = remaining / 2;
        int newNameWidth = remaining - remaining / 2;
        return [currentNameWidth, newNameWidth, .. extraWidths, folder, SizeColumnWidth, type, timeWidth, timeWidth];
    }

    private void OnListClientSizeChanged(object? sender, EventArgs e)
    {
        if (!Visible || _savedColumnWidths is not null)
        {
            return;
        }
        int width = Main.List.ClientSize.Width;
        if (width > 0 && width != _lastListWidth)
        {
            _lastListWidth = width;
            Main.List.SetColumnWidths(CalculateColumnWidths(Main.List));
        }
    }

    protected void UpdatePreview()
    {
        if (Updating)
        {
            return;
        }
        Items = Arrange(Items);
        var options = CurrentOptions();
        if (Group != ListGroup.None)
        {
            options = options with { NumberGroupKey = i => GroupKeyOf(i) ?? "" };
        }
        if (_overrideBasis != (options.Search, options.Replace))
        {
            _overrides.Clear();
            _overrideBasis = (options.Search, options.Replace);
        }
        Rows = new RenameEngine(options).Preview(Items);
        if (_overrides.Count > 0)
        {
            Rows = Rows.Select(r => _overrides.TryGetValue(r.Item.FullPath, out var name) ? r with { NewName = name } : r).ToList();
        }

        var list = Main.List;
        list.BeginUpdate();
        list.Items.Clear();
        var added = new List<(ListViewItem, FileItem)>();
        foreach (var row in Rows)
        {
            var item = row.Item;
            var lvi = new ListViewItem(item.Name, list.IconKey(item)) { Tag = row, UseItemStyleForSubItems = false };
            var newName = lvi.SubItems.Add(row.NewName);
            if (row.NewName != item.Name)
            {
                newName.Font = list.BoldFont;
            }
            foreach (var (_, value) in ExtraColumns)
            {
                lvi.SubItems.Add(value(item));
            }
            lvi.SubItems.Add(item.DirectoryPath);
            lvi.SubItems.Add(FileListView.FormatSize(item.Size));  // フォルダも「0 B」（オリジナル版と同じ）
            lvi.SubItems.Add(ShellInfo.TypeName(item.FullPath, item.IsDirectory));
            lvi.SubItems.Add(FileListView.FormatTime(item.Modified));
            lvi.SubItems.Add(FileListView.FormatTime(item.Created));
            list.Items.Add(lvi);
            added.Add((lvi, item));
        }
        ApplyGroups(list, added);
        list.EndUpdate();
        Main.UpdateCounts();
    }

    /// <summary>列の並べ替えキー：現在の名前、新しい名前（名前で代用）、フォルダ、サイズ、種類、更新日時、作成日時。</summary>
    protected override Func<FileItem, IComparable>? ColumnKey(int column)
    {
        var extras = ExtraColumns;
        if (column >= 2 && column - 2 < extras.Count)
        {
            var value = extras[column - 2].Value;
            return i => value(i);
        }
        int baseColumn = column >= 2 ? column - extras.Count : column;
        return baseColumn switch
        {
            0 or 1 => i => i.Name,
            2 => i => i.DirectoryPath,
            3 => i => i.Size,
            // 種類で並べるとフォルダが先頭に来る（explore-sort）
            4 => i => i.IsDirectory ? "" : ShellInfo.TypeName(i.FullPath, i.IsDirectory),
            5 => i => i.Modified,
            6 => i => i.Created,
            _ => null,
        };
    }

    private bool IsSizeColumn(int column)
    {
        int extras = ExtraColumns.Count;
        return column >= 2 && column - 2 >= extras && column - extras == 3;
    }

    /// <summary>サイズの列は最初のクリックで降順（explore-sort）。</summary>
    protected override bool DefaultAscending(int column) => !IsSizeColumn(column);

    /// <summary>サイズの列はもう一度クリックしても降順のまま（explore-sort）。</summary>
    protected override bool TogglesDirection(int column) => !IsSizeColumn(column);

    /// <summary>並べ替えは一覧を集め直さず、今の並びに対して行う（オリジナル版と同じく直前の並びに対して安定）。</summary>
    protected override void Rearrange() => UpdatePreview();

    private string LogPath => Path.Combine(Res.BaseDirectory, "Rename.log");

    private static bool IsToFolder(RenameOperation operation) =>
        operation is RenameOperation.MoveToFolder or RenameOperation.CopyToFolder or RenameOperation.ShortcutToFolder or RenameOperation.HardLinkToFolder;

    /// <summary>実行ボタンの ▼：最近送ったフォルダ（新しい順、選ぶとそこへ送る）と「送り先にサブフォルダを作成」（オリジナル版では常に無効）。</summary>
    private void ShowDestinationMenu()
    {
        Popup.Clear();
        var history = DestinationHistory.Get(Res.Settings);
        if (history.Count == 0)
        {
            Popup.AddItem(Res.T("Recently Destination Folder"), () => { }, enabled: false);
        }
        for (int i = 0; i < history.Count; i++)
        {
            string folder = history[i];
            Popup.AddItem($"&{i + 1} {folder}", () => ExecuteRename(folder));
        }
        Popup.AddSeparator();
        Popup.AddItem(Res.T("Create &Subfolder at Destination"), () => { }, enabled: false, isChecked: false);
        Popup.ShowBelow(Get<Button>(IdRename));
    }

    /// <param name="destination">別フォルダへの動作で、送り先が決まっているとき（最近送ったフォルダから選んだとき）。null なら選択ダイアログを出す。</param>
    private void ExecuteRename(string? destination)
    {
        var operation = (RenameOperation)Get<ComboBox>(IdOperation).SelectedIndex;
        var selected = Main.List.SelectedItems.Cast<ListViewItem>().Select(i => (PreviewRow)i.Tag!).ToList();
        // リネームでは名前の変わる項目だけ。移動・コピー・ごみ箱などは名前が変わらなくても処理する（exec-recycle の実測）
        var rows = (selected.Count > 0 ? selected : Rows).Where(r => operation != RenameOperation.Rename || r.NewName != r.Item.Name).ToList();
        if (rows.Count == 0)
        {
            return;
        }
        if (!IsToFolder(operation))
        {
            destination = null;
        }
        else if (destination is null)
        {
            // 送り先の選択は確認メッセージより先（オリジナル版の実測：explore-destination）。
            // オリジナル版と同じ SHBrowseForFolder（「フォルダーの参照」、新しいフォルダーの作成ボタン付き）を出す
            using var picker = new FolderBrowserDialog
            {
                Description = Res.T("Select Destination Folder"),
                SelectedPath = Main.CurrentFolder,
                AutoUpgradeEnabled = false,
                ShowNewFolderButton = true,
            };
            if (picker.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            destination = picker.SelectedPath;
        }
        // 選択がなければ、表示されている全部を処理してよいか確認する（オリジナル版と同じ）
        if (selected.Count == 0 && MessageBox.Show(this, Res.T("Process All File/Folder"), "Flexible Renamer", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        var options = CurrentOptions();
        CommitHistory();
        if (destination is not null)
        {
            DestinationHistory.Push(Res.Settings, destination);
        }

        using var progress = new ProgressDialog(Res, Res.T("Renaming..."));
        // 重複時の処理：オプションで「連番を付ける」なら問い合わせずに付ける
        var conflictDefaults = OptionsDialog.ConflictDefaults(Res.Settings);
        var executor = new RenameExecutor(new DialogCallbacks(Res, this, progress, conflictDefaults))
        {
            Operation = operation,
            DestinationFolder = destination,
            AlwaysAutoNumber = Res.Settings.GetInt("Rename", "DuplicateIndex", 0) == 1 ? conflictDefaults : null,
        };
        try
        {
            using var log = RenameLog.Begin(LogPath, Res.T("This file is used to UNDO, and Error recovery."), DateTime.Now, Main.CurrentFolder, options.Search, options.Replace);
            progress.Run(this, () => executor.Execute(rows, log));
        }
        catch (InvalidOperationException e)
        {
            MessageBox.Show(this, e.Message, "Flexible Renamer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        // ごみ箱へ移動は元に戻せない（オリジナル版でもボタンは無効のまま）
        Get<Button>(IdUndo).Enabled = operation != RenameOperation.RecycleBin;
        Main.RefreshList();
        // オリジナル版のダイアログバーはフォーカスを持たないので、実行後は一覧に戻す（Enter で再実行されない）
        Main.List.Focus();
    }

    private void Undo()
    {
        var entry = RenameLog.Read(LogPath);
        if (entry is null || !entry.Completed)
        {
            MessageBox.Show(this, Res.T("%s\nIllegal Log\nLast operation was probably abnormal terminated.").Replace("%s", LogPath, StringComparison.Ordinal), "Flexible Renamer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        string question = Res.T("Undo this operation?\nDate: %s\nFolder: %s\nSearch for: %s\nReplace with: %s");
        foreach (var value in new[] { entry.Time.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture), entry.Folder, entry.Search, entry.Replace })
        {
            int i = question.IndexOf("%s", StringComparison.Ordinal);
            question = question[..i] + value + question[(i + 2)..];
        }
        if (MessageBox.Show(this, question, "Flexible Renamer", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }
        int failed = RenameExecutor.Undo(entry);
        File.Delete(LogPath);
        Get<Button>(IdUndo).Enabled = false;
        MessageBox.Show(this, Res.T(failed == 0 ? "Undo Completed." : "Failured to Undo"), "Flexible Renamer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Main.RefreshList();
    }

    /// <summary>共通の設定（[セクション] の ModeExec、NumberingBase、NumberingStep、EnableSub、RecursiveDepth、UseScript）。</summary>
    protected void LoadCommonSettings(string section)
    {
        var s = Res.Settings;
        SelectScriptLanguage(s.Get(section, "ScriptLanguage", "VBScript"));
        Get<CheckBox>(IdRecursive).Checked = s.GetBool(section, "EnableSub", false);
        Get<TextBox>(IdDepth).Text = s.GetInt(section, "RecursiveDepth", 0).ToString(CultureInfo.InvariantCulture);
        Get<CheckBox>(IdScript).Checked = s.GetBool(section, "UseScript", false);
        Get<ComboBox>(IdOperation).SelectedIndex = Math.Clamp(s.GetInt(section, "ModeExec", 0), 0, 5);
        Get<TextBox>(IdStart).Text = s.GetInt(section, "NumberingBase", 0).ToString(CultureInfo.InvariantCulture);
        Get<TextBox>(IdStep).Text = s.GetInt(section, "NumberingStep", 1).ToString(CultureInfo.InvariantCulture);
        // 列の幅：「幅,列番号,0」を列ごとに並べたもの（オリジナル版の ini と同じ）。保存された幅があればそのまま使う
        var parts = (s.Get(section, "ListCtrl-1000-0-0") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var widths = new List<int>();
        for (int i = 0; i < parts.Length && i / 3 < BaseColumnCount; i += 3)
        {
            if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) && w > 0)
            {
                widths.Add(w);
            }
        }
        _savedColumnWidths = widths.Count == BaseColumnCount ? widths.ToArray() : null;
    }

    protected void SaveCommonSettings(string section)
    {
        var s = Res.Settings;
        s.Set(section, "ScriptLanguage", SelectedScriptLanguage);
        s.Set(section, "EnableSub", Get<CheckBox>(IdRecursive).Checked);
        s.Set(section, "RecursiveDepth", Get<TextBox>(IdDepth).Text);
        s.Set(section, "UseScript", Get<CheckBox>(IdScript).Checked);
        s.Set(section, "ModeExec", Get<ComboBox>(IdOperation).SelectedIndex);
        s.Set(section, "NumberingBase", Get<TextBox>(IdStart).Text);
        s.Set(section, "NumberingStep", Get<TextBox>(IdStep).Text);
        SaveColumnWidths(section);
    }

    /// <summary>一覧の基本の7列の幅を ini に書く（タグ列は除く）。ページが表示中でなければ前回読んだ値のまま。</summary>
    private void SaveColumnWidths(string section)
    {
        var columns = Main.List.Columns;
        int extras = ExtraColumns.Count;
        if (!Visible || columns.Count != BaseColumnCount + extras)
        {
            return;
        }
        var widths = new List<int>();
        for (int i = 0; i < columns.Count; i++)
        {
            if (i < 2 || i >= 2 + extras)
            {
                widths.Add(columns[i].Width);
            }
        }
        Res.Settings.Set(section, "ListCtrl-1000-0-0", string.Join(',', widths.Select((w, i) => $"{w},{i},0")) + ",0");
    }
}
