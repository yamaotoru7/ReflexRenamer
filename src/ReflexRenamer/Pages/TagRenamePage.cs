using ReflexRenamer.Core.Presets;
using ReflexRenamer.Core.Rename;
using ReflexRenamer.Core.Tags;
using ReflexRenamer.Core.Settings;
using ReflexRenamer.Dialogs;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Pages;

/// <summary>
/// タグ・リネームページ（DIALOG 170）。タグの種類を選び、その種類の拡張子のファイルを一覧にして、
/// 書式（<c>&lt;ID3.Artist&gt;</c> などのタグを含む）で新しい名前を作る。
/// </summary>
public sealed class TagRenamePage : RenamePageBase
{
    private const int IdUseProperty = 1169;
    private const int IdEditProperty = 1030;
    private const int IdKinds = 1088;
    private const int IdDescription = 1055;
    private const int IdFileTypes = 1160;
    private const int IdFileTypesLabel = 1170;
    private const int IdTagListButton = 1046;
    private const int MenuTagList = 171;
    private const int MenuOption = 173;
    private const int CmdTagListPlaceholder = 32950;
    private const int CmdTagRenameSettings = 32951;

    private readonly IReadOnlyList<TagKind> _kinds;
    private readonly Dictionary<string, TagSet> _tagCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _formatByKind = [];
    private TagFormatOptions _formatOptions = new();
    private string _formatKind = "";
    private string _knownKind = "";
    private HashSet<string> _knownTags = [];
    private int _designHeight;
    private const string PresetSection = "TagRenamePreset";
    private const string PropertyPresetSection = "TagRename_PropertyTag";
    private List<(string Name, string Format)> _presets = [];

    // プロパティモード（「ファイルプロパティ情報を使う」）
    private readonly PropertyTagDefinitions _propertyDefinitions;
    private List<PropertyPreset> _propertyPresets = [];
    private ShellColumns? _shell;
    private bool _propertyMode;

    // 見出しメニューで選んだタグ列（表示名で区別する。同じ表示名を選び直すと消える）
    private readonly List<(string Title, string Token)> _tagColumns = [];
    private readonly Dictionary<int, Rectangle> _designBounds = [];

    public TagRenamePage(AppResources res, MainForm main) : base(res, main, 170, Ui.Commands.PageTagRename)
    {
        _kinds = TagDefinitions.Load(Path.Combine(res.BaseDirectory, "TagDef.ini"));
        _propertyDefinitions = PropertyTagDefinitions.Load(Path.Combine(res.BaseDirectory, "TagDef.ini"), Environment.OSVersion.Version.Build);
        _propertyPresets = _propertyDefinitions.Presets.Select(p => p with { Name = res.T(p.Name) }).ToList();
        Initialize();
    }

    private void Initialize()
    {
        Updating = true;
        InitializeCommon();

        // 縦方向の配分（AdjustMiddle）のために設計時の位置を覚える（この時点ではテンプレートの大きさ）
        _designHeight = DialogBuilder.DesignSize(Res.Resources.Dialogs[170]).Height;
        foreach (var id in new[] { IdKinds, IdDescription, IdFileTypesLabel, IdFileTypes })
        {
            _designBounds[id] = Get<Control>(id).Bounds;
        }

        // 種類の一覧：ラジオボタンのように描く
        var kinds = Get<ListBox>(IdKinds);
        kinds.DrawMode = DrawMode.OwnerDrawFixed;
        kinds.ItemHeight = 18;
        kinds.DrawItem += DrawKind;
        foreach (var kind in _kinds)
        {
            kinds.Items.Add(Res.Strings.Expand(kind.Caption));
        }
        kinds.SelectedIndex = 0;

        var types = Get<ListView>(IdFileTypes);
        types.CheckBoxes = true;
        types.HeaderStyle = ColumnHeaderStyle.None;
        types.Columns.Add("", 80);
        types.Columns.Add("", 126);
        types.SmallImageList = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
        Updating = false;

        kinds.SelectedIndexChanged += (_, _) => { if (!Updating) { SwitchKind(); Main.RefreshList(); } };
        // ハンドル生成時にも項目ごとに ItemChecked が飛ぶ。マウス・キーボードでの変更だけ拾う
        types.ItemChecked += (_, _) => { if (!Updating && types.Focused) Main.RefreshList(); };
        Get<CheckBox>(IdUseProperty).CheckedChanged += (_, _) => { if (!Updating) { SwitchPropertyMode(Get<CheckBox>(IdUseProperty).Checked); Main.RefreshList(); } };
        Get<Button>(IdTagListButton).Click += (_, _) => ShowTagListMenu();
        Get<Button>(IdOptionButton).Click += (_, _) => ShowOptionMenu(MenuOption, HandleOption);
        Get<Button>(IdPresetButton).Click += (_, _) => ShowPresetMenu();
        Get<Label>(IdEditProperty).Click += (_, _) => EditPropertyDefinitions();
        Get<Label>(IdEditProperty).Cursor = Cursors.Hand;
        SwitchKind();
    }

    private TagKind Kind => _kinds[Math.Max(Get<ListBox>(IdKinds).SelectedIndex, 0)];

    /// <summary>プロパティモードで選んでいるプリセット。選んでいなければ null（オリジナル版は切り替え直後に何も選ばない）。</summary>
    private PropertyPreset? PropertyPreset
    {
        get
        {
            int index = Get<ListBox>(IdKinds).SelectedIndex;
            return _propertyMode && index >= 0 && index < _propertyPresets.Count ? _propertyPresets[index] : null;
        }
    }

    private ShellColumns? Shell => _shell ??= ShellColumns.Create();

    /// <summary>
    /// 「ファイルプロパティ情報を使う」の切り替え。種類の一覧をプリセット（ミュージック、写真、…）に入れ替え、
    /// 何も選ばない状態にする。説明文は PropertyTag の Description、ファイルの種類は空（explore-tagproperty）。
    /// </summary>
    private void SwitchPropertyMode(bool on)
    {
        bool was = Updating;
        Updating = true;
        _propertyMode = on;
        var kinds = Get<ListBox>(IdKinds);
        kinds.Items.Clear();
        if (on)
        {
            foreach (var preset in _propertyPresets)
            {
                kinds.Items.Add(preset.Name);
            }
            kinds.SelectedIndex = -1;
        }
        else
        {
            foreach (var kind in _kinds)
            {
                kinds.Items.Add(Res.Strings.Expand(kind.Caption));
            }
            kinds.SelectedIndex = 0;
        }
        _formatKind = "";
        Updating = was;
        SwitchKind();
    }

    private void EditPropertyDefinitions()
    {
        if (Shell is null)
        {
            return;
        }
        using var dialog = new PropertyDefinitionDialog(Res, _propertyPresets, _propertyDefinitions, Shell);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        _propertyPresets = dialog.Presets.ToList();
        Res.Settings.SetList(PropertyPresetSection, "Preset", PropertyTagDefinitions.FormatPresets(_propertyPresets));
        if (_propertyMode)
        {
            SwitchPropertyMode(true);
            Main.RefreshList();
        }
    }

    /// <summary>
    /// ペインが伸びた分の配分。オリジナル版の実測（explore-tagrename の music.tree）では、伸びた高さの 40% を
    /// 種類の一覧（1088）に足し、説明文（1055）・「ファイルの種類」（1170）・その一覧（1160）をその分だけ下げ、
    /// 一覧（1160）の下端はページの下端に付いていく（残りの 60% が 1160 に足される）。
    /// </summary>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_designBounds.Count == 0)
        {
            return;
        }
        int grow = Math.Max(ClientSize.Height - _designHeight, 0);
        int shift = grow * 2 / 5;
        var kinds = _designBounds[IdKinds];
        Get<Control>(IdKinds).SetBounds(kinds.X, kinds.Y, Get<Control>(IdKinds).Width, kinds.Height + shift);
        foreach (var id in new[] { IdDescription, IdFileTypesLabel })
        {
            var d = _designBounds[id];
            var c = Get<Control>(id);
            c.SetBounds(c.Left, d.Y + shift, c.Width, c.Height);
        }
        var types = _designBounds[IdFileTypes];
        Get<Control>(IdFileTypes).SetBounds(types.X, types.Y + shift, Get<Control>(IdFileTypes).Width, types.Height + grow - shift);
    }

    private void DrawKind(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }
        var list = (ListBox)sender!;
        e.DrawBackground();
        var glyph = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + (e.Bounds.Height - 13) / 2, 13, 13);
        bool selected = (e.State & DrawItemState.Selected) != 0;
        RadioButtonRenderer.DrawRadioButton(e.Graphics, glyph.Location,
            selected ? System.Windows.Forms.VisualStyles.RadioButtonState.CheckedNormal : System.Windows.Forms.VisualStyles.RadioButtonState.UncheckedNormal);
        TextRenderer.DrawText(e.Graphics, list.Items[e.Index].ToString(), e.Font, new Rectangle(glyph.Right + 4, e.Bounds.Top, e.Bounds.Width - glyph.Right - 4, e.Bounds.Height), SystemColors.WindowText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.DrawFocusRectangle();
    }

    /// <summary>種類が変わったとき：説明、ファイルの種類の一覧、書式（種類ごとに覚える）、履歴を切り替える。</summary>
    private void SwitchKind()
    {
        bool was = Updating;
        Updating = true;
        // 種類やモードを変えるとタグ列は消える（オリジナル版の実測）
        if (_tagColumns.Count > 0)
        {
            _tagColumns.Clear();
            ApplyColumns();
        }
        if (_formatKind.Length > 0)
        {
            _formatByKind[_formatKind] = Get<ComboBox>(IdReplace).Text;
        }
        string section;
        IEnumerable<string> extensions;
        if (_propertyMode)
        {
            section = "PropertyTag";
            extensions = PropertyPreset?.Extensions ?? [];
        }
        else
        {
            section = Kind.Section;
            extensions = Kind.Extensions;
        }
        _formatKind = section;
        Get<Label>(IdDescription).Text = Res.Strings.Lookup(section, "Description") ?? "";

        var types = Get<ListView>(IdFileTypes);
        types.BeginUpdate();
        types.Items.Clear();
        var icons = types.SmallImageList!;
        foreach (var ext in extensions)
        {
            string key = "." + ext;
            if (!icons.Images.ContainsKey(key) && ShellInfo.SmallIconForType(key) is { } icon)
            {
                icons.Images.Add(key, icon);
            }
            var item = new ListViewItem(ext, icons.Images.ContainsKey(key) ? key : "") { Checked = true };
            item.SubItems.Add(ShellInfo.TypeName("x" + key, false));
            types.Items.Add(item);
        }
        types.EndUpdate();

        MruCombo.Load(Get<ComboBox>(IdReplace), Res.Settings, section + "Replace");
        Get<ComboBox>(IdReplace).Text = _formatByKind.TryGetValue(section, out var f) ? f : "";
        Updating = was;
    }

    /// <summary>タグ一覧メニュー：先頭の「Tag」項目を、選んでいる種類のタグ（サブメニューは v1、v2 など）に置き換える。</summary>
    private void ShowTagListMenu()
    {
        var combo = Get<ComboBox>(IdReplace);
        Popup.Clear();
        AddTagItems(Popup, (_, token) => InsertAtCaret(combo, token));
        AddStandardTagItems(combo);
        Popup.ShowBelow(Get<Button>(IdTagListButton));
    }

    /// <summary>
    /// 選んでいる種類のタグをメニューに足す。通常は TagDef.ini の順（v1、v2 はサブメニュー）、
    /// プロパティモードはプリセットの列の表示名を平らに並べる。プリセットを選んでいなければ無効の「タグ」（オリジナル版と同じ）。
    /// </summary>
    private void AddTagItems(PopupMenu popup, Action<string, string> pick)
    {
        if (_propertyMode)
        {
            var preset = PropertyPreset;
            var columns = preset is null || Shell is null ? [] : preset.Columns.Select(c => Shell.NameOf(c)).Where(n => n is not null).Select(n => n!).ToList();
            if (columns.Count == 0)
            {
                popup.AddItem(Res.T("Tag"), () => { }, enabled: false);
            }
            foreach (var name in columns)
            {
                string token = "<" + name + ">";
                popup.AddItem(name, () => pick(name, token));
            }
            return;
        }
        var kind = Kind;
        foreach (var group in kind.Fields.GroupBy(f => f.Submenu))
        {
            if (group.Key is null)
            {
                foreach (var field in group)
                {
                    string label = TagLabel(kind, field);
                    string token = field.Token;
                    popup.AddItem(label + "\t" + token, () => pick(label, token));
                }
            }
            else
            {
                popup.AddSubmenu(group.Key, group.Select(field =>
                {
                    string label = TagLabel(kind, field);
                    return (label + "\t" + field.Token, (Action)(() => pick(label, field.Token)));
                }));
            }
        }
    }

    /// <summary>見出しメニュー（MENU 181 の後ろ）：タグを選ぶとその値の列が「新しい名前」の後ろに付く。もう一度選ぶと消える（C12）。</summary>
    public override void AddHeaderMenuItems(PopupMenu popup) => AddTagItems(popup, ToggleTagColumn);

    private void ToggleTagColumn(string title, string token)
    {
        int index = _tagColumns.FindIndex(c => c.Title == title);
        if (index >= 0)
        {
            _tagColumns.RemoveAt(index);
        }
        else
        {
            _tagColumns.Add((title, token));
        }
        ApplyColumns();
        UpdatePreview();
    }

    protected override IReadOnlyList<(string Title, Func<FileItem, string> Value)> ExtraColumns =>
        _tagColumns.Select(c => (c.Title, (Func<FileItem, string>)(item => ExpandTags(item, c.Token)))).ToList();

    /// <summary>タグ一覧メニューの後半：メニューリソースの項目（現在の名前、連番など）。</summary>
    private void AddStandardTagItems(ComboBox combo)
    {
        var template = Res.Resources.Menus[MenuTagList][0].Children ?? [];
        foreach (var item in template.Where(i => i.Id != CmdTagListPlaceholder))
        {
            Popup.AddFromTemplate(item, t =>
            {
                var (_, insert) = PopupMenu.Split(Res.T(t.Text));
                if (insert is not null)
                {
                    InsertAtCaret(combo, insert);
                }
            });
        }
    }

    private string TagLabel(TagKind kind, TagField field) => Res.Strings.Lookup(kind.Section, field.Token) ?? field.Name;

    /// <summary>プリセットメニュー：書式のプリセット。ないときはオリジナル版と同じく無効の「プリセット」を置く。</summary>
    private void ShowPresetMenu()
    {
        var combo = Get<ComboBox>(IdReplace);
        Popup.Clear();
        if (_presets.Count == 0)
        {
            Popup.AddItem(Res.T("Preset"), () => { }, enabled: false);
        }
        foreach (var preset in _presets)
        {
            var p = preset;
            Popup.AddItem(p.Name, () => { combo.Text = p.Format; UpdatePreview(); });
        }
        Popup.AddSeparator();
        Popup.AddItem(Res.T("&Add Current Search/Replace Setting"), AddPreset, enabled: combo.Text.Length > 0);
        Popup.AddItem(Res.T("&Order Preset"), OrderPresets);
        Popup.ShowBelow(Get<Button>(IdPresetButton));
    }

    private void AddPreset()
    {
        var combo = Get<ComboBox>(IdReplace);
        string? name = InputDialog.Show(this, Res, Res.T("New Preset"), Res.T("Preset Name"), combo.Text);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        _presets.Add((name, combo.Text));
        SavePresets();
    }

    private void OrderPresets()
    {
        using var dialog = new OrderPresetDialog(Res, Res.T("Order Preset"), _presets.Select(p => (p.Name, p.Format, (object)p)), canRename: true);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        _presets = dialog.Result.Select(r => (r.Name, (((string Name, string Format))r.Tag).Format)).ToList();
        SavePresets();
    }

    /// <summary>プリセットは ini の [TagRenamePreset] に「書式⇥名前」で保存する（オリジナル版の保存形式は未確認）。</summary>
    private void LoadPresets() => _presets = Res.Settings.GetList(PresetSection, "Preset")
        .Select(line => line.Split('	'))
        .Where(parts => parts.Length >= 2)
        .Select(parts => (parts[1], parts[0]))
        .ToList();

    private void SavePresets() => Res.Settings.SetList(PresetSection, "Preset", _presets.Select(p => p.Format + "	" + p.Name));

    private bool HandleOption(int command)
    {
        if (command != CmdTagRenameSettings)
        {
            return false;
        }
        using var dialog = new TagRenameSettingsDialog(Res, _formatOptions);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _formatOptions = dialog.Options;
            UpdatePreview();
        }
        return true;
    }

    public override void RefreshList()
    {
        if (Updating)
        {
            return;
        }
        // ハンドル生成時に項目が実体化される途中では Items に null が混ざるので OfType で除く
        _shell?.ClearValues();
        var checkedExtensions = Get<ListView>(IdFileTypes).Items.OfType<ListViewItem>().Where(i => i.Checked).Select(i => "*." + i.Text).ToList();
        Items = checkedExtensions.Count == 0 || (_propertyMode && PropertyPreset is null) ? [] : CollectItems(new CollectOptions
        {
            Filter = string.Join(';', checkedExtensions),
            Recursive = Get<CheckBox>(IdRecursive).Checked,
            Depth = Depth,
            HideProtectedOsFiles = HideProtected,
        });
        UpdatePreview();
    }

    protected override RenameOptions CurrentOptions() => new()
    {
        Mode = RenameMode.RegularExpression,
        Search = "^.*$",
        // 新しい名前は書式の展開結果に元の拡張子を付けたもの（書式が空なら「.mp3」だけになる。正解データ music-empty）
        Replace = Get<ComboBox>(IdReplace).Text + @"\e",
        NumberStart = StartNumber,
        NumberStep = StepNumber,
        NumberType = NumberType,
        ResetNumberByFolder = Get<CheckBox>(IdResetByFolder).Checked,
        UseCreationTime = UseCreationTime,
        Script = Get<CheckBox>(IdScript).Checked ? ScriptFor(SelectedScriptLanguage) : null,
        TagExpander = ExpandTags,
    };

    private string ExpandTags(FileItem item, string format)
    {
        if (!format.Contains('<'))
        {
            return format;
        }
        if (_propertyMode)
        {
            return ExpandProperties(item, format);
        }
        if (!_tagCache.TryGetValue(item.FullPath, out var tags))
        {
            tags = TagReader.Read(item.FullPath);
            _tagCache[item.FullPath] = tags;
        }
        var kind = Kind;
        if (_knownKind != kind.Section)
        {
            _knownKind = kind.Section;
            _knownTags = kind.Fields.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        return TagFormatter.Expand(format, tags, _formatOptions, key => Res.Strings.Lookup(kind.Section, key), _knownTags.Contains);
    }

    /// <summary>
    /// プロパティモードの展開。プリセットの列の表示名（<c>&lt;タイトル&gt;</c>）をエクスプローラーの「詳細」の値にする。
    /// プリセットにない名前は文字のまま。プロパティストアを開けないファイルは全部「UnknownTag」（オリジナル版と同じ）。
    /// </summary>
    private string ExpandProperties(FileItem item, string format)
    {
        var preset = PropertyPreset;
        if (preset is null || Shell is null)
        {
            return format;
        }
        var tags = new TagSet();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (int column in preset.Columns)
        {
            if (Shell.NameOf(column) is { } name)
            {
                known.Add(name);
                tags.Set(name, Shell.Value(item.FullPath, column));
            }
        }
        return TagFormatter.Expand(format, tags, _formatOptions, null, known.Contains, anyName: true);
    }

    protected override void CommitHistory() => MruCombo.Commit(Get<ComboBox>(IdReplace), Res.Settings, (_formatKind.Length > 0 ? _formatKind : Kind.Section) + "Replace");

    public override void LoadSettings()
    {
        var s = Res.Settings;
        Updating = true;
        LoadCommonSettings("TagRename");
        LoadPresets();
        var savedPresets = PropertyTagDefinitions.ParsePresets(s.GetList(PropertyPresetSection, "Preset"));
        if (savedPresets.Count > 0)
        {
            _propertyPresets = savedPresets.ToList();
        }
        Get<CheckBox>(IdUseProperty).Checked = s.GetBool("TagRename", "UseProperty", false);
        foreach (var kind in _kinds)
        {
            _formatByKind[kind.Section] = s.Get("TagRename", kind.Section.Replace("Tag", "Replace", StringComparison.Ordinal), "");
        }
        _formatOptions = new TagFormatOptions
        {
            DateTimeFormat = s.Get("TagRename", "DateTimeFormat", TagFormatOptions.DefaultDateTimeFormat),
            InvalidCharReplacements = TagRenameSettingsDialog.InvalidChars.ToDictionary(x => x.Char, x => s.Get("TagRename", "InvalidChar" + x.Id, TagFormatOptions.DefaultReplacements[x.Char])),
        };
        _propertyMode = Get<CheckBox>(IdUseProperty).Checked;
        if (_propertyMode)
        {
            SwitchPropertyMode(true);
            int mode = s.GetInt("TagRename", "PropertyMode", -1);
            Get<ListBox>(IdKinds).SelectedIndex = mode >= 0 && mode < _propertyPresets.Count ? mode : -1;
        }
        else
        {
            Get<ListBox>(IdKinds).SelectedIndex = Math.Clamp(s.GetInt("TagRename", "Mode", 0), 0, _kinds.Count - 1);
        }
        _formatKind = "";
        Updating = false;
        SwitchKind();
        UpdateRecursiveEnabled();
    }

    public override void SaveSettings()
    {
        var s = Res.Settings;
        _formatByKind[_formatKind.Length > 0 ? _formatKind : Kind.Section] = Get<ComboBox>(IdReplace).Text;
        SaveCommonSettings("TagRename");
        s.Set("TagRename", "UseProperty", Get<CheckBox>(IdUseProperty).Checked);
        s.Set("TagRename", _propertyMode ? "PropertyMode" : "Mode", Get<ListBox>(IdKinds).SelectedIndex);
        s.Set(AppSettings.General, "OSVersion_PropertyTag", _propertyDefinitions.Version);
        s.SetList(PropertyPresetSection, "Preset", PropertyTagDefinitions.FormatPresets(_propertyPresets));
        foreach (var (section, format) in _formatByKind)
        {
            s.Set("TagRename", section.Replace("Tag", "Replace", StringComparison.Ordinal), format);
        }
        s.Set("TagRename", "DateTimeFormat", _formatOptions.DateTimeFormat);
        foreach (var (c, id) in TagRenameSettingsDialog.InvalidChars)
        {
            s.Set("TagRename", "InvalidChar" + id, _formatOptions.InvalidCharReplacements.TryGetValue(c, out var r) ? r : "");
        }
    }
}
