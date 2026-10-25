using System.Globalization;
using ReflexRenamer.Core.Attributes;
using ReflexRenamer.Core.Rename;
using ReflexRenamer.Dialogs;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Pages;

/// <summary>属性ページ（DIALOG 141）：タイムスタンプと属性の変更。</summary>
public sealed class AttributePage : PageBase
{
    private const int IdFile = 1033;
    private const int IdFolder = 1034;
    private const int IdRecursive = 1024;
    private const int IdMode = 1017;
    private const int IdCreated = 1040;
    private const int IdUpdated = 1042;
    private const int IdAccessed = 1043;
    private const int IdEarlier = 1143;
    private const int IdLater = 1144;
    private const int IdDays = 1125;
    private const int IdTime = 1047;
    private const int IdMin = 1045;
    private const int IdMax = 1046;
    private const int IdBaseCreated = 1139;
    private const int IdBaseUpdated = 1140;
    private const int IdBaseAccessed = 1141;
    private const int IdBasePhoto = 1142;
    private const int IdApplyCreated = 1121;
    private const int IdApplyUpdated = 1122;
    private const int IdApplyAccessed = 1123;
    private const int IdFolderTimestamp = 1025;
    private const int IdReadOnly = 1004;
    private const int IdArchive = 1005;
    private const int IdHidden = 1008;
    private const int IdSystem = 1012;
    private const int IdStart = 1011;

    /// <summary>方式ごとに表示する入力欄（重なって配置されている）。</summary>
    private static readonly Dictionary<TimestampMode, int[]> ModeControls = new()
    {
        [TimestampMode.Specify] = [1118, 1040, 1119, 1042, 1120, 1043],
        [TimestampMode.Shift] = [1143, 1144, 1126, 1125, 1133, 1127, 1047, 1130, 1121, 1122, 1123],
        [TimestampMode.Random] = [1128, 1045, 1129, 1046, 1130, 1121, 1122, 1123],
        [TimestampMode.Sequential] = [1128, 1045, 1143, 1144, 1126, 1125, 1133, 1127, 1047, 1130, 1121, 1122, 1123],
        [TimestampMode.MakeSame] = [1124, 1139, 1140, 1141, 1142, 1130, 1121, 1122, 1123],
    };

    private readonly ControlMap _map;
    private List<FileItem> _items = [];
    private bool _updating;

    public AttributePage(AppResources res, MainForm main) : base(res, main)
    {
        var template = res.Resources.Dialogs[141];
        Size = DialogBuilder.DesignSize(template);
        _map = res.Builder.Build(141, template, this);
        Tooltips.Apply(141, _map, res, new ToolTip());
        _map[1000].Visible = false;
        _map[1001].Visible = false;
        _map.Get<PictureBox>(1009).Image = Assets.Icon(Ui.Commands.PageAttribute, 32)?.ToBitmap();

        _updating = true;
        Get<CheckBox>(IdFile).Checked = true;
        Get<CheckBox>(IdRecursive).Checked = true;
        var mode = Get<ComboBox>(IdMode);
        mode.Items.AddRange([res.T("Specify DateTime"), res.T("Shift"), res.T("Random Generate"), res.T("Sequential Generate"), res.T("Make the Same")]);
        mode.SelectedIndex = 0;

        // 日時の欄は「日付 (曜日) 時刻」のカスタム書式（オリジナル版の表示）
        // 日時の欄は「短い日付 長い時刻」（一覧と同じ書式。日本語ではユーザー設定で曜日が付く）。アクセス日時は日付だけ
        var f = CultureInfo.CurrentCulture.DateTimeFormat;
        foreach (var id in new[] { IdCreated, IdUpdated, IdAccessed, IdMin, IdMax })
        {
            var picker = Get<DateTimePicker>(id);
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = id == IdAccessed ? f.ShortDatePattern : f.ShortDatePattern + " " + f.LongTimePattern;
            // チェックが外れた状態では値を設定しても表示が変わらないので、付けてから設定して外す
            picker.Checked = true;
            // 下限（連続生成の開始）の既定は前日の 0:00、上限は当日の 0:00（オリジナル版の実測）
            picker.Value = id == IdMin ? DateTime.Today.AddDays(-1) : DateTime.Today;
        }
        foreach (var id in new[] { IdCreated, IdUpdated, IdAccessed })
        {
            Get<DateTimePicker>(id).Checked = false;
        }
        var time = Get<DateTimePicker>(IdTime);
        time.Format = DateTimePickerFormat.Custom;
        time.CustomFormat = "H:mm:ss";
        time.ShowUpDown = true;
        time.Value = DateTime.Today;
        Get<TextBox>(IdDays).Text = "0";
        Get<RadioButton>(IdEarlier).Checked = true;
        Get<RadioButton>(IdBaseCreated).Checked = true;
        foreach (var id in new[] { IdReadOnly, IdArchive, IdHidden, IdSystem })
        {
            Get<CheckBox>(id).CheckState = CheckState.Indeterminate;
        }
        _updating = false;

        foreach (var id in new[] { IdFile, IdFolder, IdRecursive })
        {
            Get<CheckBox>(id).CheckedChanged += (_, _) => { UpdateFolderTimestampEnabled(); Main.RefreshList(); };
        }
        UpdateFolderTimestampEnabled();
        mode.SelectedIndexChanged += (_, _) => UpdateMode();
        foreach (var id in new[] { IdBaseCreated, IdBaseUpdated, IdBaseAccessed, IdBasePhoto })
        {
            Get<RadioButton>(id).CheckedChanged += (_, _) => UpdateApplyEnabled();
        }
        Get<Button>(IdStart).Click += (_, _) => Execute();
        Main.List.ClientSizeChanged += OnListClientSizeChanged;
        _shiftRowTop = _map[IdEarlier].Top;
        UpdateMode();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Main.List.ClientSizeChanged -= OnListClientSizeChanged;
        }
        base.Dispose(disposing);
    }

    private T Get<T>(int id) where T : Control => _map.Get<T>(id);

    public override void LoadSettings()
    {
        var s = Res.Settings;
        _updating = true;
        Get<ComboBox>(IdMode).SelectedIndex = Math.Clamp(s.GetInt("Attribute", "Mode", 0), 0, 4);
        Get<RadioButton>(s.GetInt("Attribute", "Shift", 0) == 0 ? IdEarlier : IdLater).Checked = true;
        Get<TextBox>(IdDays).Text = s.GetInt("Attribute", "ShiftDate", 0).ToString(CultureInfo.InvariantCulture);
        if (TimeSpan.TryParse(s.Get("Attribute", "ShiftTime"), CultureInfo.InvariantCulture, out var shift))
        {
            Get<DateTimePicker>(IdTime).Value = DateTime.Today + shift;
        }
        if (DateTime.TryParse(s.Get("Attribute", "RandomMin"), CultureInfo.CurrentCulture, DateTimeStyles.None, out var min))
        {
            Get<DateTimePicker>(IdMin).Value = min;
        }
        if (DateTime.TryParse(s.Get("Attribute", "RandomMax"), CultureInfo.CurrentCulture, DateTimeStyles.None, out var max))
        {
            Get<DateTimePicker>(IdMax).Value = max;
        }
        Get<RadioButton>(IdBaseCreated + Math.Clamp(s.GetInt("Attribute", "Same", 0), 0, 3)).Checked = true;
        _updating = false;
        UpdateMode();
    }

    public override void SaveSettings()
    {
        var s = Res.Settings;
        s.Set("Attribute", "Mode", Get<ComboBox>(IdMode).SelectedIndex);
        s.Set("Attribute", "Shift", Get<RadioButton>(IdLater).Checked ? 1 : 0);
        s.Set("Attribute", "ShiftDate", Get<TextBox>(IdDays).Text);
        s.Set("Attribute", "ShiftTime", Get<DateTimePicker>(IdTime).Value.ToString("H:mm:ss", CultureInfo.InvariantCulture));
        s.Set("Attribute", "RandomMin", FileListView.FormatTime(Get<DateTimePicker>(IdMin).Value));
        s.Set("Attribute", "RandomMax", FileListView.FormatTime(Get<DateTimePicker>(IdMax).Value));
        s.Set("Attribute", "Same", Get<RadioButton>(IdBaseUpdated).Checked ? 1 : Get<RadioButton>(IdBaseAccessed).Checked ? 2 : Get<RadioButton>(IdBasePhoto).Checked ? 3 : 0);
    }

    private TimestampMode Mode => (TimestampMode)Get<ComboBox>(IdMode).SelectedIndex;

    /// <summary>シフトの入力欄（前に/後に、日数、時間）の設計時の上端。連続生成では「開始」の行を入れるため1行分下げる。</summary>
    private int _shiftRowTop;

    private static readonly int[] ShiftRow = [1143, 1144, 1126, 1125, 1133, 1127, 1047];

    private void UpdateMode()
    {
        var visible = ModeControls[Mode].ToHashSet();
        foreach (var id in ModeControls.Values.SelectMany(v => v).Distinct())
        {
            if (_map.TryGet(id, out var c))
            {
                c.Visible = visible.Contains(id);
            }
        }
        // 連続生成では、下限の欄が開始日時になり、シフトの行はその下（実測で 22px 下）に移る
        int shiftOffset = Mode == TimestampMode.Sequential ? 22 : 0;
        foreach (var id in ShiftRow)
        {
            var c = _map[id];
            c.Top = _shiftRowTop + RowDelta(id) + shiftOffset;
        }
        _map[1128].Text = Res.T(Mode == TimestampMode.Sequential ? "Start" : "Min") + ":";
        UpdateApplyEnabled();
    }

    /// <summary>シフトの行の中での、前に/後に の行からの縦のずれ（設計時の位置関係を保つ）。</summary>
    private readonly Dictionary<int, int> _rowDelta = [];

    private int RowDelta(int id)
    {
        if (_rowDelta.Count == 0)
        {
            foreach (var i in ShiftRow)
            {
                _rowDelta[i] = _map[i].Top - _map[IdEarlier].Top;
            }
        }
        return _rowDelta[id];
    }

    /// <summary>「フォルダのタイムスタンプも変更する」は、フォルダが対象になるときだけ選べる。</summary>
    private void UpdateFolderTimestampEnabled() =>
        // オリジナル版は「フォルダのタイムスタンプも変更する」を常に押せるままにしている（exec-attribute の実測）
        Get<CheckBox>(IdFolderTimestamp).Enabled = true;

    /// <summary>「揃える」では、元となる日時と同じ適用先は選べない。</summary>
    private void UpdateApplyEnabled()
    {
        bool same = Mode == TimestampMode.MakeSame;
        Get<CheckBox>(IdApplyCreated).Enabled = !(same && Get<RadioButton>(IdBaseCreated).Checked);
        Get<CheckBox>(IdApplyUpdated).Enabled = !(same && Get<RadioButton>(IdBaseUpdated).Checked);
        Get<CheckBox>(IdApplyAccessed).Enabled = !(same && Get<RadioButton>(IdBaseAccessed).Checked);
    }

    private const int MinNameColumnWidth = 120;
    private const int FolderColumnWidth = 180;
    private const int SizeColumnWidth = 72;
    private const int TypeColumnWidth = 100;
    private const int AttributeColumnWidth = 56;
    private int _lastListWidth;

    public override void Activate()
    {
        var list = Main.List;
        _lastListWidth = list.ClientSize.Width;
        var w = CalculateColumnWidths(list);
        list.SetColumns(
            (Res.T("Name"), w[0], HorizontalAlignment.Left),
            (Res.T("Folder"), w[1], HorizontalAlignment.Left),
            (Res.T("Size"), w[2], HorizontalAlignment.Right),
            (Res.T("Type"), w[3], HorizontalAlignment.Left),
            (Res.T("Date Updated"), w[4], HorizontalAlignment.Left),
            (Res.T("Date Created"), w[5], HorizontalAlignment.Left),
            (Res.T("Date Accessed"), w[6], HorizontalAlignment.Left),
            (Res.T("Attribute"), w[7], HorizontalAlignment.Left));
        base.Activate();
    }

    private static int[] CalculateColumnWidths(FileListView list)
    {
        int timeWidth = list.MeasureTimeColumnWidth();
        int folder = FolderColumnWidth;
        int type = TypeColumnWidth;
        // 狭いときは情報量の少ない列から縮める（フォルダ → 種類 → 日時）。RenamePageBase と同じ規則
        int Fixed() => folder + SizeColumnWidth + type + timeWidth * 3 + AttributeColumnWidth;
        int deficit = MinNameColumnWidth - (list.ClientSize.Width - Fixed());
        FileListView.Shrink(ref folder, 60, ref deficit);
        FileListView.Shrink(ref type, 70, ref deficit);
        FileListView.Shrink(ref timeWidth, 96, ref deficit, count: 3);
        int nameWidth = Math.Max(list.ClientSize.Width - Fixed(), MinNameColumnWidth);
        return [nameWidth, folder, SizeColumnWidth, type, timeWidth, timeWidth, timeWidth, AttributeColumnWidth];
    }

    private void OnListClientSizeChanged(object? sender, EventArgs e)
    {
        if (!Visible)
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

    public override void RefreshList()
    {
        if (_updating)
        {
            return;
        }
        // 一覧は直下だけ。フォルダは対象にするとき、またはサブフォルダ以下をたどるとき（中に入る入口として）に出す（実測）
        var collect = new CollectOptions
        {
            Files = Get<CheckBox>(IdFile).Checked,
            Folders = Get<CheckBox>(IdFolder).Checked || Get<CheckBox>(IdRecursive).Checked,
        };
        _items = Main.FileMode ? FileCollector.CollectSpecified(Main.SpecifiedPaths, collect) : FileCollector.Collect(Main.CurrentFolder, collect);
        _items = Arrange(_items);
        var list = Main.List;
        list.BeginUpdate();
        list.Items.Clear();
        var added = new List<(ListViewItem, FileItem)>();
        foreach (var item in _items)
        {
            var lvi = new ListViewItem(item.Name, list.IconKey(item)) { Tag = item };
            added.Add((lvi, item));
            lvi.SubItems.Add(FolderText(item));
            // フォルダも「0 B」、アクセス日時は日付だけ（実測）
            lvi.SubItems.Add(FileListView.FormatSize(item.Size));
            lvi.SubItems.Add(ShellInfo.TypeName(item.FullPath, item.IsDirectory));
            lvi.SubItems.Add(FileListView.FormatTime(item.Modified));
            lvi.SubItems.Add(FileListView.FormatTime(item.Created));
            lvi.SubItems.Add(item.Accessed.ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern, CultureInfo.CurrentCulture));
            lvi.SubItems.Add(AttributeLetters(item.FullPath));
            list.Items.Add(lvi);
        }
        ApplyGroups(list, added);
        list.EndUpdate();
        Main.UpdateCounts();
    }

    /// <summary>列の並べ替えキー：名前、フォルダ、サイズ、種類、更新、作成、アクセス、属性。</summary>
    protected override Func<FileItem, IComparable>? ColumnKey(int column) => column switch
    {
        0 => i => i.Name,
        1 => i => i.DirectoryPath,
        2 => i => i.Size,
        3 => i => ShellInfo.TypeName(i.FullPath, i.IsDirectory),
        4 => i => i.Modified,
        5 => i => i.Created,
        6 => i => i.Accessed,
        7 => i => AttributeLetters(i.FullPath),
        _ => null,
    };

    /// <summary>エクスプローラーと同じ属性の文字（R H S A）。</summary>
    private static string AttributeLetters(string path)
    {
        FileAttributes a;
        try
        {
            a = File.GetAttributes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
        var sb = new System.Text.StringBuilder();
        if ((a & FileAttributes.ReadOnly) != 0) sb.Append('R');
        if ((a & FileAttributes.Hidden) != 0) sb.Append('H');
        if ((a & FileAttributes.System) != 0) sb.Append('S');
        if ((a & FileAttributes.Archive) != 0) sb.Append('A');
        return sb.ToString();
    }

    private AttributeOptions Options()
    {
        static AttributeChange Change(CheckBox c) => c.CheckState switch
        {
            CheckState.Checked => AttributeChange.Set,
            CheckState.Unchecked => AttributeChange.Clear,
            _ => AttributeChange.Keep,
        };
        static DateTime? Picked(DateTimePicker p) => p.Checked ? p.Value : null;
        int.TryParse(Get<TextBox>(IdDays).Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int days);
        return new AttributeOptions
        {
            Mode = Mode,
            Created = Picked(Get<DateTimePicker>(IdCreated)),
            Updated = Picked(Get<DateTimePicker>(IdUpdated)),
            Accessed = Picked(Get<DateTimePicker>(IdAccessed)),
            ShiftEarlier = Get<RadioButton>(IdEarlier).Checked,
            ShiftDays = days,
            ShiftTime = Get<DateTimePicker>(IdTime).Value.TimeOfDay,
            RangeMin = Get<DateTimePicker>(IdMin).Value,
            RangeMax = Get<DateTimePicker>(IdMax).Value,
            SequenceStart = Get<DateTimePicker>(IdMin).Value,
            SequenceStep = TimeSpan.FromDays(days) + Get<DateTimePicker>(IdTime).Value.TimeOfDay,
            BaseKind = Get<RadioButton>(IdBaseUpdated).Checked ? TimestampKind.Updated
                : Get<RadioButton>(IdBaseAccessed).Checked ? TimestampKind.Accessed
                : Get<RadioButton>(IdBasePhoto).Checked ? TimestampKind.PhotoTaken
                : TimestampKind.Created,
            ApplyToCreated = Get<CheckBox>(IdApplyCreated).Checked,
            ApplyToUpdated = Get<CheckBox>(IdApplyUpdated).Checked,
            ApplyToAccessed = Get<CheckBox>(IdApplyAccessed).Checked,
            ModifyFolderTimestamp = Get<CheckBox>(IdFolderTimestamp).Checked,
            PhotoTakenReader = path => Core.Tags.TagReader.Read(path)["EXIF.OriginalTime"].Time,
            ReadOnly = Change(Get<CheckBox>(IdReadOnly)),
            Archive = Change(Get<CheckBox>(IdArchive)),
            Hidden = Change(Get<CheckBox>(IdHidden)),
            System = Change(Get<CheckBox>(IdSystem)),
        };
    }

    private void Execute()
    {
        var selected = Main.List.SelectedItems.Cast<ListViewItem>().Select(i => (FileItem)i.Tag!).ToList();
        var targets = selected.Count > 0 ? selected : _items;
        if (targets.Count == 0)
        {
            MessageBox.Show(this, Res.T("No Target File"), "Flexible Renamer");
            return;
        }
        // サブフォルダ以下も対象なら、一覧のフォルダの中身を実行時に広げる
        if (Get<CheckBox>(IdRecursive).Checked)
        {
            var expanded = new List<FileItem>();
            bool files = Get<CheckBox>(IdFile).Checked;
            bool folders = Get<CheckBox>(IdFolder).Checked;
            foreach (var t in targets)
            {
                if (t.IsDirectory)
                {
                    if (folders) expanded.Add(t);
                    expanded.AddRange(FileCollector.Collect(t.FullPath, new CollectOptions { Files = files, Folders = folders, Recursive = true }));
                }
                else
                {
                    expanded.Add(t);
                }
            }
            targets = expanded;
        }
        var plans = AttributeChanger.Plan(targets, Options());
        using var progress = new ProgressDialog(Res, Res.T("Modifing Attributes..."));
        progress.Run(this, () => AttributeChanger.Execute(plans, progress.Report));
        Main.RefreshList();
        // オリジナル版のダイアログバーはフォーカスを持たないので、実行後は一覧に戻す（Enter で再実行されない）
        Main.List.Focus();
    }
}
