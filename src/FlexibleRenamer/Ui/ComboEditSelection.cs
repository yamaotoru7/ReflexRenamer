using System.Runtime.InteropServices;

namespace FlexibleRenamer.Ui;

/// <summary>コンボの編集窓がフォーカスを失う前に選択範囲を保持する。UIA が子 Edit に直接フォーカスした場合にも対応。</summary>
internal sealed class ComboEditSelection : NativeWindow, IDisposable
{
    private readonly ComboBox _combo;
    public (int Start, int Length) Selection { get; private set; }

    public ComboEditSelection(ComboBox combo)
    {
        _combo = combo;
        combo.HandleCreated += Attach;
        combo.HandleDestroyed += Detach;
        combo.TextChanged += TextChanged;
        if (combo.IsHandleCreated) Attach(combo, EventArgs.Empty);
    }

    private void Attach(object? sender, EventArgs e)
    {
        var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
        if (GetComboBoxInfo(_combo.Handle, ref info) && info.Edit != 0) AssignHandle(info.Edit);
    }

    private void Detach(object? sender, EventArgs e)
    {
        if (Handle != 0) ReleaseHandle();
    }

    private void TextChanged(object? sender, EventArgs e)
    {
        if (!_combo.ContainsFocus) Selection = (0, 0);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0008) // WM_KILLFOCUS: WinForms に選択範囲を戻される前に読む
        {
            SendMessage(Handle, 0x00B0, out int start, out int end); // EM_GETSEL
            Selection = (start, end - start);
        }
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        _combo.HandleCreated -= Attach;
        _combo.HandleDestroyed -= Detach;
        _combo.TextChanged -= TextChanged;
        Detach(null, EventArgs.Empty);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct ComboBoxInfo
    {
        public int Size;
        public Rect Item, Button;
        public int ButtonState;
        public nint Combo, Edit, List;
    }
    [DllImport("user32")] private static extern bool GetComboBoxInfo(nint hwnd, ref ComboBoxInfo info);
    [DllImport("user32")] private static extern nint SendMessage(nint hwnd, uint message, out int start, out int end);
}
