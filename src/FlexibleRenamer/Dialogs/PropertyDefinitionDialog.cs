using FlexibleRenamer.Core.Tags;
using FlexibleRenamer.Ui;

namespace FlexibleRenamer.Dialogs;

/// <summary>
/// プロパティ定義（DIALOG 174）。「ファイルプロパティ情報を使う」のプリセット（名前、フィルタ、使う列）を編集する。
/// 左の一覧でプリセットを選び、右で名前・フィルタ・列のチェックを変える。列はグループ（TagDef.ini の PropertyTagGroup）で分けて表示する。
/// </summary>
public sealed class PropertyDefinitionDialog : TemplateForm
{
    private const int IdPresets = 1088;
    private const int IdName = 1048;
    private const int IdFilter = 1057;
    private const int IdColumns = 1165;
    private const int IdUp = 1051;
    private const int IdDown = 1052;
    private const int IdNew = 1166;
    private const int IdDelete = 1053;

    private readonly List<PropertyPreset> _presets;
    private readonly ListView _list;
    private readonly ListView _columns;
    private bool _updating;

    public PropertyDefinitionDialog(AppResources res, IReadOnlyList<PropertyPreset> presets, PropertyTagDefinitions definitions, ShellColumns shell) : base(res, 174)
    {
        _presets = presets.ToList();
        _list = Get<ListView>(IdPresets);
        _list.HeaderStyle = ColumnHeaderStyle.None;
        _list.Columns.Add("", _list.ClientSize.Width - 4);
        _list.MultiSelect = false;

        _columns = Get<ListView>(IdColumns);
        _columns.HeaderStyle = ColumnHeaderStyle.None;
        _columns.CheckBoxes = true;
        _columns.ShowGroups = true;
        // 横スクロールバーが出ないように、縦スクロールバーの分を引いた幅にする
        _columns.Columns.Add("", _columns.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        BuildColumns(res, definitions, shell);

        foreach (var p in _presets)
        {
            _list.Items.Add(p.Name);
        }

        _list.SelectedIndexChanged += (_, _) => ShowSelected();
        Get<TextBox>(IdName).TextChanged += (_, _) => Update(p => p with { Name = Get<TextBox>(IdName).Text });
        Get<TextBox>(IdFilter).TextChanged += (_, _) => Update(p => p with { Filter = Get<TextBox>(IdFilter).Text });
        _columns.ItemChecked += (_, _) => Update(p => p with { Columns = CheckedColumns() });
        Get<Button>(IdUp).Click += (_, _) => MovePreset(-1);
        Get<Button>(IdDown).Click += (_, _) => MovePreset(1);
        Get<Button>(IdNew).Click += (_, _) => AddNew(res);
        Get<Button>(IdDelete).Click += (_, _) => DeleteSelected();
        if (_list.Items.Count > 0)
        {
            _list.Items[0].Selected = true;
        }
        ShowSelected();
    }

    /// <summary>編集後のプリセット。</summary>
    public IReadOnlyList<PropertyPreset> Presets => _presets;

    private int Selected => _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] : -1;

    /// <summary>
    /// 列をグループごとに、列番号順に並べる。同じ列が複数のグループに書かれているときは最後のグループに入る
    /// （オリジナル版の実測：195「フォルダー名」は Description ではなく Origin、279「彩度」は Video ではなく Origin）。
    /// グループにない列は「その他」。名前のない列番号は出さない。
    /// </summary>
    private void BuildColumns(AppResources res, PropertyTagDefinitions definitions, ShellColumns shell)
    {
        var groupOf = new Dictionary<int, PropertyGroup>();
        foreach (var group in definitions.Groups.Where(g => !g.IsOther))
        {
            foreach (int c in group.Columns)
            {
                groupOf[c] = group;
            }
        }
        _columns.BeginUpdate();
        foreach (var group in definitions.Groups)
        {
            var lvGroup = new ListViewGroup(res.Strings.Lookup("PropertyTagGroup", group.Name) ?? group.Name);
            _columns.Groups.Add(lvGroup);
            foreach (var (index, name) in shell.Names)
            {
                bool belongs = groupOf.TryGetValue(index, out var g) ? g == group : group.IsOther;
                if (belongs)
                {
                    _columns.Items.Add(new ListViewItem("<" + name + ">", lvGroup) { Tag = index });
                }
            }
        }
        _columns.EndUpdate();
    }

    private void ShowSelected()
    {
        _updating = true;
        int index = Selected;
        var preset = index >= 0 ? _presets[index] : null;
        Get<TextBox>(IdName).Text = preset?.Name ?? "";
        Get<TextBox>(IdFilter).Text = preset?.Filter ?? "";
        var selected = preset?.Columns.ToHashSet() ?? [];
        foreach (ListViewItem item in _columns.Items)
        {
            item.Checked = selected.Contains((int)item.Tag!);
        }
        bool enabled = preset is not null;
        Get<TextBox>(IdName).Enabled = enabled;
        Get<TextBox>(IdFilter).Enabled = enabled;
        _columns.Enabled = enabled;
        // オリジナル版は「上へ」「下へ」「削除」を常に押せるままにしている
        _updating = false;
    }

    private void Update(Func<PropertyPreset, PropertyPreset> change)
    {
        int index = Selected;
        if (_updating || index < 0)
        {
            return;
        }
        _presets[index] = change(_presets[index]);
        _list.Items[index].Text = _presets[index].Name;
    }

    private List<int> CheckedColumns() =>
        _columns.Items.OfType<ListViewItem>().Where(i => i.Checked).Select(i => (int)i.Tag!).OrderBy(c => c).ToList();

    private void MovePreset(int delta)
    {
        int index = Selected;
        int target = index + delta;
        if (index < 0 || target < 0 || target >= _presets.Count)
        {
            return;
        }
        (_presets[index], _presets[target]) = (_presets[target], _presets[index]);
        _list.Items[index].Text = _presets[index].Name;
        _list.Items[target].Text = _presets[target].Name;
        _list.Items[target].Selected = true;
        ShowSelected();
    }

    private void AddNew(AppResources res)
    {
        _presets.Add(new PropertyPreset(res.T("New Preset"), "", []));
        var item = _list.Items.Add(_presets[^1].Name);
        item.Selected = true;
        ShowSelected();
        Get<TextBox>(IdName).Focus();
    }

    private void DeleteSelected()
    {
        int index = Selected;
        if (index < 0)
        {
            return;
        }
        _presets.RemoveAt(index);
        _list.Items.RemoveAt(index);
        if (_list.Items.Count > 0)
        {
            _list.Items[Math.Min(index, _list.Items.Count - 1)].Selected = true;
        }
        ShowSelected();
    }
}
