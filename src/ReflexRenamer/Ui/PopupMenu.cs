using System.Runtime.InteropServices;
using ReflexRenamer.Core.Resources;

namespace ReflexRenamer.Ui;

/// <summary>
/// ボタンから出すポップアップメニュー。オリジナル版と同じ Win32 のメニュー（TrackPopupMenu）を使うので、
/// 見た目（タブより後ろの文字列が右寄せで出る）も、自動操作から見えるウィンドウ（クラス #32768）も同じになる。
/// 項目はメニューリソース（<c>_WildSearch_</c> などのサブメニュー1つ）から作る。
/// </summary>
public sealed class PopupMenu(AppResources res)
{
    private const uint MfString = 0x0;
    private const uint MfSeparator = 0x800;
    private const uint MfPopup = 0x10;
    private const uint MfChecked = 0x8;
    private const uint MfGrayed = 0x1;
    private const uint TpmReturnCmd = 0x100;
    private const uint TpmLeftButton = 0x0;

    private readonly List<Entry> _entries = [];

    private sealed record Entry(int Id, string Text, bool Separator, bool Checked, bool Enabled, Action? OnClick, List<Entry>? Children);

    public void Clear() => _entries.Clear();

    /// <summary>メニューリソースの id 番目のサブメニューの中身で項目を作り直す。</summary>
    public void Load(int menuId, int submenuIndex, Action<MenuTemplate> onClick)
    {
        _entries.Clear();
        var popup = res.Resources.Menus[menuId][submenuIndex];
        foreach (var item in popup.Children ?? [])
        {
            _entries.Add(Create(item, onClick));
        }
    }

    private Entry Create(MenuTemplate item, Action<MenuTemplate> onClick)
    {
        if (item.Separator)
        {
            return new Entry(0, "", true, false, true, null, null);
        }
        var children = item.Children?.Select(c => Create(c, onClick)).ToList();
        return new Entry(item.Id, res.T(item.Text), false, false, true, children is null ? () => onClick(item) : null, children);
    }

    public void AddItem(string text, Action onClick, bool enabled = true, bool isChecked = false) =>
        _entries.Add(new Entry(0, text, false, isChecked, enabled, onClick, null));

    public void AddSeparator() => _entries.Add(new Entry(0, "", true, false, true, null, null));

    /// <summary>サブメニューを足す（項目は「ラベル\t右側の文字列」と処理の組）。</summary>
    public void AddSubmenu(string text, IEnumerable<(string Text, Action OnClick)> items) =>
        _entries.Add(new Entry(0, text, false, false, true, null, items.Select(i => new Entry(0, i.Text, false, false, true, i.OnClick, null)).ToList()));

    public void AddSubmenu(string text, IEnumerable<(string Text, Action OnClick, bool Checked)> items) =>
        _entries.Add(new Entry(0, text, false, false, true, null, items.Select(i => new Entry(0, i.Text, false, i.Checked, true, i.OnClick, null)).ToList()));

    /// <summary>メニューリソースの項目（サブメニューを含む）を足す。</summary>
    public void AddFromTemplate(MenuTemplate item, Action<MenuTemplate> onClick) => _entries.Add(Create(item, onClick));

    /// <summary>コマンド ID の項目にチェックを付ける（サブメニューの中も探す）。</summary>
    public void SetChecked(int commandId, bool value) => Replace(_entries, commandId, e => e with { Checked = value });

    private static void Replace(List<Entry> entries, int id, Func<Entry, Entry> f)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Id == id && entries[i].Children is null)
            {
                entries[i] = f(entries[i]);
            }
            else if (entries[i].Children is { } children)
            {
                Replace(children, id, f);
            }
        }
    }

    /// <summary>「ラベル\t挿入文字列」を分ける。</summary>
    public static (string Label, string? Insert) Split(string text)
    {
        int tab = text.IndexOf('\t');
        return tab < 0 ? (text, null) : (text[..tab], text[(tab + 1)..]);
    }

    /// <summary>ボタンの下に表示し、選ばれた項目の処理を呼ぶ。</summary>
    public void ShowBelow(Control button) => ShowAt(button.FindForm()!, button.PointToScreen(new Point(0, button.Height)));

    /// <summary>画面座標の位置に表示する。</summary>
    public void ShowAt(Form owner, Point screen)
    {
        var actions = new Dictionary<int, Action>();
        nint menu = Build(_entries, actions);
        try
        {
            var p = screen;
            int selected = TrackPopupMenuEx(menu, TpmReturnCmd | TpmLeftButton, p.X, p.Y, owner.Handle, 0);
            if (selected != 0 && actions.TryGetValue(selected, out var action))
            {
                action();
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private static nint Build(List<Entry> entries, Dictionary<int, Action> actions)
    {
        nint menu = CreatePopupMenu();
        foreach (var e in entries)
        {
            if (e.Separator)
            {
                AppendMenu(menu, MfSeparator, 0, null);
                continue;
            }
            uint flags = MfString | (e.Checked ? MfChecked : 0) | (e.Enabled ? 0 : MfGrayed);
            if (e.Children is not null)
            {
                AppendMenu(menu, flags | MfPopup, Build(e.Children, actions), e.Text);
            }
            else
            {
                // 戻り値で項目を見分けるため、1 から番号を振る（リソースの ID は 0 の項目もあるので使わない）
                int id = actions.Count + 1;
                actions[id] = e.OnClick ?? (() => { });
                AppendMenu(menu, flags, id, e.Text);
            }
        }
        return menu;
    }

    [DllImport("user32")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(nint menu, uint flags, nint idOrSubmenu, string? text);

    [DllImport("user32")]
    private static extern int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);

    [DllImport("user32")]
    private static extern bool DestroyMenu(nint menu);
}
