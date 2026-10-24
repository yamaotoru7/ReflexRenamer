using System.Runtime.InteropServices;
using System.Text;

namespace FrTools;

/// <summary>UIA が子メニューを公開しない場合にも Win32 メニューの項目を読めるようにする。</summary>
internal static class NativePopup
{
    internal sealed record Item(int Position, string Text, bool Separator, bool Enabled, bool Checked, nint Submenu);

    public static nint Handle(nint window) => SendMessage(window, 0x01E1, 0, 0); // MN_GETHMENU

    public static System.Drawing.Point Point(nint menu, int position)
    {
        if (!GetMenuItemRect(0, menu, (uint)position, out var rect))
            throw new InvalidOperationException("メニュー項目の位置を取得できない");
        return new((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32")] private static extern bool GetMenuItemRect(nint window, nint menu, uint position, out Rect rect);

    public static IReadOnlyList<Item> Read(nint menu)
    {
        var result = new List<Item>();
        for (int i = 0; i < GetMenuItemCount(menu); i++)
        {
            var text = new StringBuilder(2048);
            GetMenuString(menu, (uint)i, text, text.Capacity, 0x400);
            uint state = GetMenuState(menu, (uint)i, 0x400);
            nint submenu = GetSubMenu(menu, i);
            // ポップアップの上位バイトは子項目数であり、MF_SEPARATOR などのフラグではない。
            result.Add(new Item(i, text.ToString(), submenu == 0 && (state & 0x800) != 0, (state & 3) == 0, (state & 8) != 0, submenu));
        }
        return result;
    }

    [DllImport("user32")] private static extern nint SendMessage(nint hwnd, uint msg, nint wparam, nint lparam);
    [DllImport("user32")] private static extern int GetMenuItemCount(nint menu);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetMenuString(nint menu, uint item, StringBuilder text, int count, uint flags);
    [DllImport("user32")] private static extern uint GetMenuState(nint menu, uint item, uint flags);
    [DllImport("user32")] private static extern nint GetSubMenu(nint menu, int position);
}
