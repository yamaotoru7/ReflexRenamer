using FlexibleRenamer.Core.Presets;
using FlexibleRenamer.Ui;

namespace FlexibleRenamer.Dialogs;

/// <summary>プリセットの整理（DIALOG 139）：並べ替え、名前の変更、削除。お気に入りの整理にも使う。</summary>
public sealed class OrderPresetDialog : TemplateForm
{
    private const int IdList = 1088;
    private const int IdHigher = 1051;
    private const int IdLower = 1052;
    private const int IdModifyName = 1054;
    private const int IdDelete = 1053;

    private readonly ListView _list;

    public OrderPresetDialog(AppResources res, string title, IEnumerable<(string Name, string Detail, object Tag)> items, bool canRename) : base(res, 139)
    {
        Text = title;
        _list = Get<ListView>(IdList);
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.Columns.Add(res.T("Name"), 120);
        _list.Columns.Add(res.T("Search") + " / " + res.T("Replace"), 200);
        foreach (var (name, detail, tag) in items)
        {
            var item = new ListViewItem(name) { Tag = tag };
            item.SubItems.Add(detail);
            _list.Items.Add(item);
        }
        Get<Button>(IdHigher).Click += (_, _) => MoveSelected(-1);
        Get<Button>(IdLower).Click += (_, _) => MoveSelected(1);
        Get<Button>(IdModifyName).Enabled = canRename;
        Get<Button>(IdModifyName).Click += (_, _) => Rename();
        Get<Button>(IdDelete).Click += (_, _) =>
        {
            if (_list.SelectedItems.Count > 0)
            {
                _list.Items.Remove(_list.SelectedItems[0]);
            }
        };
    }

    /// <summary>整理後の項目（名前と元の Tag）。</summary>
    public IEnumerable<(string Name, object Tag)> Result => _list.Items.Cast<ListViewItem>().Select(i => (i.Text, i.Tag!));

    private void MoveSelected(int delta)
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }
        var item = _list.SelectedItems[0];
        int index = item.Index + delta;
        if (index < 0 || index >= _list.Items.Count)
        {
            return;
        }
        _list.Items.Remove(item);
        _list.Items.Insert(index, item);
        item.Selected = true;
        item.Focused = true;
    }

    private void Rename()
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }
        var item = _list.SelectedItems[0];
        string? name = InputDialog.Show(this, Res, Res.T("Modify Name"), Res.T("Preset Name"), item.Text);
        if (!string.IsNullOrWhiteSpace(name))
        {
            item.Text = name;
        }
    }
}
