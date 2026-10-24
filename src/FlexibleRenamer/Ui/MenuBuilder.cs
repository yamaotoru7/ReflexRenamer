using FlexibleRenamer.Core.Resources;

namespace FlexibleRenamer.Ui;

/// <summary>
/// オリジナル版のメニューリソースから ToolStripMenuItem を組み立てる。
/// 項目の Tag にコマンド ID を入れ、クリックされたら onCommand を呼ぶ。
/// </summary>
public static class MenuBuilder
{
    public static ToolStripItem[] Build(IEnumerable<MenuTemplate> items, AppResources res, Action<int> onCommand) =>
        items.Select(i => Create(i, res, onCommand)).ToArray();

    private static ToolStripItem Create(MenuTemplate item, AppResources res, Action<int> onCommand)
    {
        if (item.Separator)
        {
            return new ToolStripSeparator();
        }

        // 「&Undo\tCtrl+Z」のようにタブの後ろはショートカットキーの表示
        string text = res.T(item.Text);
        string? shortcut = null;
        int tab = text.IndexOf('\t');
        if (tab >= 0)
        {
            shortcut = text[(tab + 1)..];
            text = text[..tab];
        }

        var menuItem = new ToolStripMenuItem(text)
        {
            Tag = item.Id,
            ShortcutKeyDisplayString = shortcut,
        };
        if (item.Children is not null)
        {
            menuItem.DropDownItems.AddRange(Build(item.Children, res, onCommand));
        }
        else
        {
            menuItem.Click += (_, _) => onCommand(item.Id);
        }
        return menuItem;
    }

    /// <summary>コマンド ID で項目を探す（チェック状態や有効・無効を切り替えるため）。</summary>
    public static ToolStripMenuItem? Find(ToolStripItemCollection items, int id) =>
        Find(items, id, m => m.Tag is int tag && tag == id);

    public static ToolStripMenuItem? Find(ToolStripItemCollection items, int id, Func<ToolStripMenuItem, bool> matches)
    {
        foreach (ToolStripItem i in items)
        {
            if (i is not ToolStripMenuItem m)
            {
                continue;
            }
            if (matches(m) && !m.HasDropDownItems)
            {
                return m;
            }
            if (Find(m.DropDownItems, id, matches) is { } found)
            {
                return found;
            }
        }
        return null;
    }
}
