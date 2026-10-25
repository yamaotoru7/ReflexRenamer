using ReflexRenamer.Core.Presets;
using ReflexRenamer.Core.Rename;
using ReflexRenamer.Dialogs;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Pages;

/// <summary>
/// リネームページ（DIALOG 143）。「高度なリネーム」がオフのときは定型メニュー（DIALOG 146）を、
/// オンのときは検索・置換の欄を表示する。入力が変わるたびにプレビュー（新しい名前の列）を作り直す。
/// </summary>
public sealed class RenamePage : RenamePageBase
{
    // DIALOG 143 のコントロール ID
    private const int IdFile = 1033;
    private const int IdFolder = 1034;
    private const int IdFilter = 1057;

    /// <summary>フィルタ欄の既定の履歴（上から順）。</summary>
    private static readonly string[] DefaultFilters =
        ["*", "bmp;dib;gif;jpg;jpe*;png;ico;cur", "htm*;htx;asp;alx;stm;shtml", "txt;doc;xls;ppt;vsd", "exe;dll;ocx"];
    private const int IdAdvanced = 1025;
    private const int IdTypicalFrame = 1016;
    private const int IdWildcard = 1037;
    private const int IdRegex = 1038;
    private const int IdTranslation = 1039;
    private const int IdSearch = 1010;
    private const int IdHint = 1055;
    private const int IdRegexOptionButton = 1051;
    private const int IdSearchSupport = 1045;
    private const int IdReplaceSupport = 1046;

    /// <summary>下端の区切り線。定型メニューのパネルはここまで広がる。</summary>
    private const int IdBottomSeparator = 1029;

    private const int MenuSearchSupport = 135;
    private const int MenuReplaceSupport = 136;
    private const int MenuRegexOption = 141;
    private const int MenuOption = 183;
    private const int CmdExample = 32800;
    private const int CmdReplaceExample = 32840;
    private const int CmdCompleteMatchOnly = 32903;
    private const int CmdCaseSensitive = 32901;

    /// <summary>高度なリネームのときだけ表示するコントロール。</summary>
    private static readonly int[] AdvancedOnly =
        [1048, 1035, 1037, 1038, 1039, 1051, 1036, 1045, 1010, 1040, 1046, 1019, 1055, 1026, 1047, 1081, 1065, 1140, 1054, 1084, 1083, 1067, 1142, 1173];

    private readonly TypicalPanel _typical;
    private readonly PresetFile _presets;
    private readonly PresetStore _presetStore;
    private bool _caseSensitive;
    private bool _completeMatchOnly;

    public RenamePage(AppResources res, MainForm main) : base(res, main, 143, Ui.Commands.PageRename)
    {
        _presets = PresetFile.Load(Path.Combine(res.BaseDirectory, "Preset.ini"));
        _presetStore = PresetStore.Load(res.Settings, _presets, res.Strings);

        // 定型メニュー（DIALOG 146）は、DIALOG 143 の枠（ID 1016）の上端から下端の区切り線まで、幅いっぱいに入れる
        var frame = Map[IdTypicalFrame];
        _typical = new TypicalPanel(res, _presets);
        _typical.Bounds = new Rectangle(0, frame.Top, Width, Map[IdBottomSeparator].Top - frame.Top);
        _typical.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _typical.Changed += UpdatePreview;
        _typical.EnterAdvancedRequested += EnterAdvancedWithTypical;
        Controls.Add(_typical);
        _typical.BringToFront();

        Initialize();
    }

    private void Initialize()
    {
        Updating = true;
        Get<CheckBox>(IdFile).Checked = true;
        Get<ComboBox>(IdFilter).Text = "*";
        Get<RadioButton>(IdWildcard).Checked = true;
        InitializeCommon();
        Updating = false;

        // 一覧を作り直す必要がある変更
        foreach (var id in new[] { IdFile, IdFolder })
        {
            Get<CheckBox>(id).CheckedChanged += (_, _) => Main.RefreshList();
        }
        Get<ComboBox>(IdFilter).TextChanged += (_, _) => Main.RefreshList();

        // プレビューだけ作り直せばよい変更
        Get<CheckBox>(IdAdvanced).CheckedChanged += (_, _) => { UpdateMode(); UpdatePreview(); };
        foreach (var id in new[] { IdWildcard, IdRegex, IdTranslation })
        {
            Get<RadioButton>(id).CheckedChanged += (_, _) => { SwitchModeText(); UpdateHint(); UpdateEnabled(); UpdatePreview(); };
        }
        Get<ComboBox>(IdSearch).TextChanged += (_, _) => UpdatePreview();
        Get<CheckBox>(IdScript).CheckedChanged += (_, _) => UpdateHint();

        // ボタンから出るメニュー
        Get<Button>(IdPresetButton).Click += (_, _) => ShowPresetMenu();
        Get<Button>(IdRegexOptionButton).Click += (_, _) => ShowRegexOptionMenu();
        Get<Button>(IdSearchSupport).Click += (_, _) => ShowSupportMenu(IdSearchSupport, IdSearch);
        Get<Button>(IdReplaceSupport).Click += (_, _) => ShowSupportMenu(IdReplaceSupport, IdReplace);
        Get<Button>(IdOptionButton).Click += (_, _) => ShowOptionMenu(MenuOption);

        UpdateMode();
        UpdateHint();
        UpdateEnabled();
    }

    private RenameMode CurrentMode =>
        Get<RadioButton>(IdRegex).Checked ? RenameMode.RegularExpression
        : Get<RadioButton>(IdTranslation).Checked ? RenameMode.CharacterRange
        : RenameMode.Wildcard;

    /// <summary>検索・置換の入力サポート。リネーム方法に合ったサブメニューを出し、選んだ項目の文字列をカーソル位置に挿入する。</summary>
    private void ShowSupportMenu(int buttonId, int comboId)
    {
        bool search = buttonId == IdSearchSupport;
        int menuId = search ? MenuSearchSupport : MenuReplaceSupport;
        int submenu = CurrentMode switch
        {
            RenameMode.CharacterRange => search ? 2 : 1,
            RenameMode.RegularExpression => search ? 1 : 0,
            _ => 0,
        };
        var combo = Get<ComboBox>(comboId);
        Popup.Load(menuId, submenu, item =>
        {
            var (label, insert) = PopupMenu.Split(Res.T(item.Text));
            if (item.Id is CmdExample or CmdReplaceExample)
            {
                // 「指定例: *.???」は例をそのまま入れる
                int colon = label.IndexOf(':');
                combo.Text = colon >= 0 ? label[(colon + 1)..].Trim() : label;
                return;
            }
            if (insert is not null)
            {
                InsertAtCaret(combo, insert);
            }
        });
        Popup.ShowBelow(Get<Button>(buttonId));
    }

    private void ShowRegexOptionMenu()
    {
        Popup.Load(MenuRegexOption, 0, item =>
        {
            if (item.Id == CmdCompleteMatchOnly) _completeMatchOnly = !_completeMatchOnly;
            if (item.Id == CmdCaseSensitive) _caseSensitive = !_caseSensitive;
            UpdatePreview();
        });
        Popup.SetChecked(CmdCompleteMatchOnly, _completeMatchOnly);
        Popup.SetChecked(CmdCaseSensitive, _caseSensitive);
        Popup.ShowBelow(Get<Button>(IdRegexOptionButton));
    }

    /// <summary>プリセットのメニュー：設定ファイルの [Preset] の逆順の一覧、現在の設定を追加、整理。</summary>
    private void ShowPresetMenu()
    {
        Popup.Clear();
        foreach (var preset in _presetStore.Presets.Reverse())
        {
            var p = preset;
            Popup.AddItem(p.Name, () => ApplyPreset(p));
        }
        Popup.AddSeparator();
        Popup.AddItem(Res.T("&Add Current Search/Replace Setting"), AddPreset, enabled: Get<ComboBox>(IdSearch).Text.Length > 0);
        Popup.AddItem(Res.T("&Order Preset"), OrderPresets);
        Popup.ShowBelow(Get<Button>(IdPresetButton));
    }

    private void OrderPresets()
    {
        using var dialog = new OrderPresetDialog(Res, Res.T("Order Preset"),
            _presetStore.Presets.Select(p => (p.Name, p.Search + " → " + p.Replace, (object)p)), canRename: true);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        _presetStore.Replace(dialog.Result.Select(r => ((Preset)r.Tag) with { Name = r.Name }));
        _presetStore.Save(Res.Settings);
    }

    /// <summary>
    /// 定型メニューで選んでいる項目を展開した検索・置換・方法を高度なリネームの欄に入れて、高度なリネームに切り替える
    /// （explore-typical-advanced：「拡張子を小文字」→ ワイルドカード、「*.*」、「.\L」）。
    /// </summary>
    private void EnterAdvancedWithTypical()
    {
        if (_typical.Expand() is { } typical)
        {
            ApplyPreset(new Preset(typical.Mode, typical.Script, typical.Search, typical.Replace, ""));
        }
        Get<CheckBox>(IdAdvanced).Checked = true;
    }

    private void ApplyPreset(Preset preset)
    {
        Updating = true;
        Get<RadioButton>(preset.Mode switch
        {
            RenameMode.RegularExpression => IdRegex,
            RenameMode.CharacterRange => IdTranslation,
            _ => IdWildcard,
        }).Checked = true;
        Get<ComboBox>(IdSearch).Text = Res.Strings.Expand(preset.Search);
        Get<ComboBox>(IdReplace).Text = Res.Strings.Expand(preset.Replace);
        if (preset.Script.Length > 0) SelectScriptLanguage(preset.Script);
        Get<CheckBox>(IdScript).Checked = preset.Script.Length > 0;
        Updating = false;
        UpdateHint();
        UpdatePreview();
    }

    private void AddPreset()
    {
        string? name = InputDialog.Show(this, Res, Res.T("New Preset"), Res.T("Preset Name"), Get<ComboBox>(IdSearch).Text);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        _presetStore.Add(new Preset(CurrentMode, Get<CheckBox>(IdScript).Checked ? SelectedScriptLanguage : "", Get<ComboBox>(IdSearch).Text, Get<ComboBox>(IdReplace).Text, name));
        _presetStore.Save(Res.Settings);
    }

    /// <summary>リネーム方法ごとの検索・置換の履歴セクション。</summary>
    private static (string Search, string Replace) MruSections(RenameMode mode) => mode switch
    {
        RenameMode.RegularExpression => ("RegExpSearch", "RegExpReplace"),
        RenameMode.CharacterRange => ("TransSearch", "TransReplace"),
        _ => ("WildCardSearch", "WildCardReplace"),
    };

    /// <summary>方法ごとに覚えている検索・置換の文字列（[Rename] の WildCardSearch など）。</summary>
    private readonly Dictionary<RenameMode, (string Search, string Replace)> _modeText = [];
    private RenameMode _textMode = RenameMode.Wildcard;

    /// <summary>リネーム方法が変わったとき、前の方法の文字列を覚えて新しい方法の文字列と履歴を出す。</summary>
    private void SwitchModeText()
    {
        var mode = CurrentMode;
        if (mode == _textMode)
        {
            return;
        }
        if (Enum.IsDefined(_textMode))
        {
            _modeText[_textMode] = (Get<ComboBox>(IdSearch).Text, Get<ComboBox>(IdReplace).Text);
        }
        _textMode = mode;
        var (searchSection, replaceSection) = MruSections(mode);
        bool was = Updating;
        Updating = true;
        MruCombo.Load(Get<ComboBox>(IdSearch), Res.Settings, searchSection);
        MruCombo.Load(Get<ComboBox>(IdReplace), Res.Settings, replaceSection);
        var text = _modeText.TryGetValue(mode, out var t) ? t : (Search: "", Replace: "");
        Get<ComboBox>(IdSearch).Text = text.Search;
        Get<ComboBox>(IdReplace).Text = text.Replace;
        Updating = was;
    }

    public override void LoadSettings()
    {
        var s = Res.Settings;
        Updating = true;
        Get<CheckBox>(IdFile).Checked = s.GetBool("Rename", "SelectFile", true);
        Get<CheckBox>(IdFolder).Checked = s.GetBool("Rename", "SelectFolder", false);
        Get<CheckBox>(IdAdvanced).Checked = s.GetBool("Rename", "AdvancedRename", false);
        LoadCommonSettings("Rename");
        if (s.GetMru("RenameFilter").Count == 0)
        {
            // 履歴がなければオリジナル版と同じ既定の候補を入れる（OnInitDialog に埋め込まれた拡張子の組）
            foreach (var v in DefaultFilters.Reverse())
            {
                s.PushMru("RenameFilter", v);
            }
        }
        MruCombo.Load(Get<ComboBox>(IdFilter), s, "RenameFilter");
        Get<ComboBox>(IdFilter).Text = s.GetMru("RenameFilter").FirstOrDefault() ?? "*";
        _modeText[RenameMode.Wildcard] = (s.Get("Rename", "WildCardSearch", ""), s.Get("Rename", "WildCardReplace", ""));
        _modeText[RenameMode.RegularExpression] = (s.Get("Rename", "RegExpSearch", ""), s.Get("Rename", "RegExpReplace", ""));
        _modeText[RenameMode.CharacterRange] = (s.Get("Rename", "TransSearch", ""), s.Get("Rename", "TransReplace", ""));
        var mode = (RenameMode)Math.Clamp(s.GetInt("Rename", "Mode", 0), 0, 2);
        Get<RadioButton>(mode switch { RenameMode.RegularExpression => IdRegex, RenameMode.CharacterRange => IdTranslation, _ => IdWildcard }).Checked = true;
        _textMode = (RenameMode)(-1);
        _typical.LoadSettings(s);
        Updating = false;
        SwitchModeText();
        UpdateMode();
        UpdateHint();
        UpdateEnabled();
    }

    public override void SaveSettings()
    {
        var s = Res.Settings;
        _presetStore.Save(s);
        _modeText[CurrentMode] = (Get<ComboBox>(IdSearch).Text, Get<ComboBox>(IdReplace).Text);
        s.Set("Rename", "SelectFile", Get<CheckBox>(IdFile).Checked);
        s.Set("Rename", "SelectFolder", Get<CheckBox>(IdFolder).Checked);
        s.Set("Rename", "AdvancedRename", Get<CheckBox>(IdAdvanced).Checked);
        s.Set("Rename", "Mode", (int)CurrentMode);
        SaveCommonSettings("Rename");
        foreach (var (mode, (search, replace)) in _modeText)
        {
            var (searchSection, replaceSection) = MruSections(mode);
            s.Set("Rename", searchSection, search);
            s.Set("Rename", replaceSection, replace);
        }
        s.PushMru("RenameFilter", Get<ComboBox>(IdFilter).Text);
        _typical.SaveSettings(s);
    }

    protected override void CommitHistory()
    {
        var (searchSection, replaceSection) = MruSections(CurrentMode);
        MruCombo.Commit(Get<ComboBox>(IdSearch), Res.Settings, searchSection);
        MruCombo.Commit(Get<ComboBox>(IdReplace), Res.Settings, replaceSection);
        MruCombo.Commit(Get<ComboBox>(IdFilter), Res.Settings, "RenameFilter");
        _typical.CommitHistory(Res.Settings);
    }

    private bool Advanced => Get<CheckBox>(IdAdvanced).Checked;

    private void UpdateMode()
    {
        bool advanced = Advanced;
        foreach (var id in AdvancedOnly)
        {
            if (Map.TryGet(id, out var c))
            {
                c.Visible = advanced;
            }
        }
        _typical.Visible = !advanced;
    }

    private void UpdateHint()
    {
        string key = Get<CheckBox>(IdScript).Checked
            ? "Meta-expressions are intactly treated as syntax of script. To deal as a string, put in double-quotations(Ex.\"\\0\")."
            : Get<RadioButton>(IdRegex).Checked
                ? @"Bracket-RegExp-Matched Substrings Equals to \1-\9"
                : Get<RadioButton>(IdTranslation).Checked
                    ? "Replace All Chars in Search List\nwith Equivalent Char in Replace List"
                    : @"Wildcard(*, ?)-Matched Substrings Equals to \1-\9";
        Get<Label>(IdHint).Text = Res.T(key);
    }

    private void UpdateEnabled()
    {
        // 正規表現オプションのボタンは正規表現のときだけ
        Get<Button>(IdRegexOptionButton).Enabled = CurrentMode == RenameMode.RegularExpression;
    }

    public override void RefreshList()
    {
        if (Updating)
        {
            return;
        }
        Items = CollectItems(new CollectOptions
        {
            Files = Get<CheckBox>(IdFile).Checked,
            Folders = Get<CheckBox>(IdFolder).Checked,
            Filter = Get<ComboBox>(IdFilter).Text,
            Recursive = Get<CheckBox>(IdRecursive).Checked,
            Depth = Depth,
            HideProtectedOsFiles = HideProtected,
        });
        UpdatePreview();
    }

    /// <summary>現在の入力から高度なリネームの設定を作る。定型メニューなら項目のテンプレートを展開する。</summary>
    protected override RenameOptions CurrentOptions()
    {
        if (!Advanced)
        {
            if (_typical.Expand() is not { } typical)
            {
                return new RenameOptions();
            }
            return new RenameOptions
            {
                Mode = typical.Mode,
                Search = typical.Search,
                Replace = typical.Replace,
                NumberStart = typical.NumberStart,
                NumberType = NumberType,
                UseCreationTime = UseCreationTime,
                Script = typical.Script.Length > 0 ? ScriptFor(typical.Script) : null,
            };
        }

        return new RenameOptions
        {
            Mode = CurrentMode,
            Search = Get<ComboBox>(IdSearch).Text,
            Replace = Get<ComboBox>(IdReplace).Text,
            NumberStart = StartNumber,
            NumberStep = StepNumber,
            NumberType = NumberType,
            ResetNumberByFolder = Get<CheckBox>(IdResetByFolder).Checked,
            CaseSensitive = _caseSensitive,
            CompleteMatchOnly = _completeMatchOnly,
            UseCreationTime = UseCreationTime,
            Script = Get<CheckBox>(IdScript).Checked ? ScriptFor(SelectedScriptLanguage) : null,
        };
    }
}
