using System.Globalization;
using FlexibleRenamer.Core.Presets;
using FlexibleRenamer.Ui;

namespace FlexibleRenamer.Pages;

/// <summary>
/// 定型メニュー（DIALOG 146）。カテゴリ別のツリーと、選んだ項目が使うパラメータ欄。
/// </summary>
public sealed class TypicalPanel : UserControl
{
    // DIALOG 146 のコントロール ID と、Preset.ini の Param ビットの対応
    private const int IdTree = 1014;
    private const int ParamAdd = 0x01;
    private const int ParamNumber = 0x02;
    private const int ParamSourceDestination = 0x04;
    private const int ParamTimestamp = 0x08;
    private const int ParamPosition = 0x10;
    private const int ParamDigits = 0x20;
    private const int ParamValue = 0x40;

    private static readonly (int Bit, int[] Ids)[] ParamControls =
    [
        (ParamAdd, [1021, 1018]),
        (ParamNumber, [1081, 1065, 1140, 1023, 1019, 1136]),
        (ParamSourceDestination, [1024, 1025, 1026, 1027]),
        (ParamTimestamp, [1028, 1044, 1022, 1045]),
        (ParamPosition, [1030, 1031, 1138]),
        (ParamDigits, [1023, 1019, 1136]),
        (ParamValue, [1082, 1066, 1141]),
    ];

    /// <summary>カテゴリ番号（Preset.ini の Category 列）と見出し。</summary>
    private static readonly string[] Categories =
    [
        "Insert Number or String",
        "Remove Number or String",
        "Translate Letters",
        "Replace String",
        "Organize Number Value",
        "Modify Extension",
    ];

    private readonly ControlMap _map;
    private readonly PresetFile _presets;
    private readonly AppResources _res;

    public TypicalPanel(AppResources res, PresetFile presets)
    {
        _res = res;
        _presets = presets;
        Font = AppResources.UiFont;
        var template = res.Resources.Dialogs[146];
        Size = DialogBuilder.DesignSize(template);
        _map = res.Builder.Build(146, template, this);
        Tooltips.Apply(146, _map, res, new ToolTip());
        var tree = _map.Get<TreeView>(IdTree);

        BuildTree(tree);
        SetDefaults();
        UpdateParams();

        tree.AfterSelect += (_, _) => { UpdateParams(); Changed?.Invoke(); };
        // 「高度なリネーム (選択されたメニュー項目)」（SS_NOTIFY の Static）：クリックで、選んでいる項目の検索・置換を持って高度なリネームに入る
        var enterAdvanced = _map.Get<Label>(1032);
        enterAdvanced.Cursor = Cursors.Hand;
        enterAdvanced.Click += (_, _) => EnterAdvancedRequested?.Invoke();
        foreach (var id in new[] { 1018, 1045, 1025, 1027 })
        {
            _map.Get<ComboBox>(id).TextChanged += (_, _) => { UpdateSample(); Changed?.Invoke(); };
        }
        foreach (var id in new[] { 1065, 1019, 1031, 1066 })
        {
            _map.Get<TextBox>(id).TextChanged += (_, _) => Changed?.Invoke();
        }
    }

    public event Action? Changed;

    /// <summary>「高度なリネーム (選択されたメニュー項目)」がクリックされた。</summary>
    public event Action? EnterAdvancedRequested;

    private void BuildTree(TreeView tree)
    {
        // アイコンは exe のアイコン 311〜316（カテゴリ：追加・削除・文字種・置換・数値・拡張子）と 310（項目の赤い丸）
        var images = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
        for (int id = 311; id <= 316; id++)
        {
            images.Images.Add(Assets.Icon(id, 16)?.ToBitmap() ?? new Bitmap(16, 16));
        }
        images.Images.Add(Assets.Icon(310, 16)?.ToBitmap() ?? new Bitmap(16, 16));
        tree.ImageList = images;
        int item = images.Images.Count - 1;
        // 項目は太字（オリジナル版の見た目）
        var bold = new Font(tree.Font, FontStyle.Bold);

        tree.BeginUpdate();
        for (int c = 0; c < Categories.Length; c++)
        {
            var category = new TreeNode(_res.T(Categories[c])) { ImageIndex = c, SelectedImageIndex = c };
            for (int i = 0; i < _presets.Typicals.Count; i++)
            {
                var typical = _presets.Typicals[i];
                if (typical.Category == c + 1)
                {
                    category.Nodes.Add(new TreeNode(_res.Strings.TranslatePreset(typical.Name)) { Tag = i, ImageIndex = item, SelectedImageIndex = item, NodeFont = bold });
                }
            }
            tree.Nodes.Add(category);
        }
        tree.EndUpdate();
        // エクスプローラーと同じ見た目（▸ の展開ボタン、薄い青の選択）。オリジナル版も同じテーマで描かれている
        // 長い項目は右端で切れ、横のスクロールバーは出ない（オリジナル版の見た目。TVS_NOHSCROLL）
        void Apply()
        {
            SetWindowTheme(tree.Handle, "Explorer", null);
            SetWindowLong(tree.Handle, -16, GetWindowLong(tree.Handle, -16) | 0x8000);
        }
        if (tree.IsHandleCreated)
        {
            Apply();
        }
        else
        {
            tree.HandleCreated += (_, _) => Apply();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint hwnd, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint hwnd, int index, int value);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(nint hwnd, string? appName, string? idList);

    private void SetDefaults()
    {
        var d = new TypicalParameters();
        _map.Get<TextBox>(1065).Text = d.StartNumber.ToString(CultureInfo.InvariantCulture);
        _map.Get<TextBox>(1019).Text = d.Digits.ToString(CultureInfo.InvariantCulture);
        _map.Get<TextBox>(1031).Text = d.Position.ToString(CultureInfo.InvariantCulture);
        _map.Get<TextBox>(1066).Text = d.Value.ToString(CultureInfo.InvariantCulture);
        _map.Get<ComboBox>(1045).Text = d.TimeFormat;
    }

    /// <summary>[Rename] の Typical_* と、追加文字列・置換前後・日時の書式の履歴を読む。</summary>
    public void LoadSettings(Core.Settings.AppSettings s)
    {
        _map.Get<TextBox>(1065).Text = s.GetInt("Rename", "NumberingBase", 0).ToString(CultureInfo.InvariantCulture);
        _map.Get<TextBox>(1031).Text = s.GetInt("Rename", "Typical_Pos", 0).ToString(CultureInfo.InvariantCulture);
        MruCombo.Load(_map.Get<ComboBox>(1018), s, "RenameTypical_Add");
        MruCombo.Load(_map.Get<ComboBox>(1025), s, "RenameTypical_Src");
        MruCombo.Load(_map.Get<ComboBox>(1027), s, "RenameTypical_Dst");
        MruCombo.Load(_map.Get<ComboBox>(1045), s, "Timestamp");
        _map.Get<ComboBox>(1018).Text = s.Get("Rename", "Typical_Add", "");
        _map.Get<ComboBox>(1025).Text = s.Get("Rename", "Typical_Src", "");
        _map.Get<ComboBox>(1027).Text = s.Get("Rename", "Typical_Dst", "");
        _map.Get<ComboBox>(1045).Text = s.Get("Rename", "Typical_Timestamp", new TypicalParameters().TimeFormat);
        int index = s.GetInt("Rename", "Typical_Index", -1);
        var tree = _map.Get<TreeView>(IdTree);
        foreach (TreeNode category in tree.Nodes)
        {
            foreach (TreeNode node in category.Nodes)
            {
                if (node.Tag is int i && i == index)
                {
                    tree.SelectedNode = node;
                    node.EnsureVisible();
                }
            }
        }
    }

    public void SaveSettings(Core.Settings.AppSettings s)
    {
        s.Set("Rename", "Typical_Index", _map.Get<TreeView>(IdTree).SelectedNode?.Tag is int i ? i : -1);
        s.Set("Rename", "Typical_Pos", _map[1031].Text);
        s.Set("Rename", "Typical_Add", _map[1018].Text);
        s.Set("Rename", "Typical_Src", _map[1025].Text);
        s.Set("Rename", "Typical_Dst", _map[1027].Text);
        s.Set("Rename", "Typical_Timestamp", _map[1045].Text);
    }

    public void CommitHistory(Core.Settings.AppSettings s)
    {
        MruCombo.Commit(_map.Get<ComboBox>(1018), s, "RenameTypical_Add");
        MruCombo.Commit(_map.Get<ComboBox>(1025), s, "RenameTypical_Src");
        MruCombo.Commit(_map.Get<ComboBox>(1027), s, "RenameTypical_Dst");
        MruCombo.Commit(_map.Get<ComboBox>(1045), s, "Timestamp");
    }

    private TypicalPreset? Selected =>
        _map.Get<TreeView>(IdTree).SelectedNode?.Tag is int index ? _presets.Typicals[index] : null;

    /// <summary>選んだ項目が使うパラメータ欄だけを表示する。</summary>
    private void UpdateParams()
    {
        int param = Selected?.Param ?? 0;
        var visible = ParamControls.Where(p => (param & p.Bit) != 0).SelectMany(p => p.Ids).ToHashSet();
        foreach (var id in ParamControls.SelectMany(p => p.Ids).Distinct())
        {
            if (_map.TryGet(id, out var c))
            {
                c.Visible = visible.Contains(id);
            }
        }
        // ツリーの下端はパラメータ欄（パネルの下端に固定）から決める。欄があれば欄の 2px 上、なければ欄の下端の 3px 上まで伸ばす
        // （オリジナル版の実測：欄ありで高さ 290、なしで 332）
        var tree = _map.Get<TreeView>(IdTree);
        var paramBounds = ParamControls.SelectMany(p => p.Ids).Select(id => _map.TryGet(id, out var c) ? c.Bounds : Rectangle.Empty).Where(r => !r.IsEmpty).ToList();
        int bottom = visible.Count == 0 ? paramBounds.Max(r => r.Bottom) - 3 : paramBounds.Min(r => r.Top) - 2;
        tree.Height = bottom - tree.Top;
        // 「高度なリネーム (選択されたメニュー項目)」は、項目を選ぶとリンクの青、選んでいないと灰色
        _map[1032].ForeColor = Selected is null ? SystemColors.GrayText : SystemColors.HotTrack;
        UpdateSample();
    }


    /// <summary>日時の書式のサンプル（「2026/10/24 (土) 1:13:36 => 20261024011336」）。</summary>
    private void UpdateSample()
    {
        var now = DateTime.Now;
        var item = new Core.Rename.FileItem("x", false, 0, now, now, now);
        string formatted = Core.Rename.TagExpander.Expand(_map.Get<ComboBox>(1045).Text, item, new Core.Rename.RenameOptions());
        _map.Get<Label>(1044).Text = $"{FileListView.FormatTime(now)} => {formatted}";
    }

    /// <summary>選んだ項目をパラメータで展開する。項目を選んでいなければ null。</summary>
    public ExpandedTypical? Expand()
    {
        if (Selected is not { } typical)
        {
            return null;
        }
        static long Long(Control c) => long.TryParse(c.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        var parameters = new TypicalParameters
        {
            AddString = _map[1018].Text,
            StartNumber = Long(_map[1065]),
            Digits = (int)Long(_map[1019]),
            Position = (int)Long(_map[1031]),
            Value = Long(_map[1066]),
            TimeFormat = _map[1045].Text,
            Source = _map[1025].Text,
            Destination = _map[1027].Text,
        };
        return TypicalTemplate.Expand(typical, parameters, _res.Strings);
    }
}
