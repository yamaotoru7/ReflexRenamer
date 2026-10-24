namespace FlexibleRenamer.Ui;

/// <summary>
/// 浮かせたペインを入れる小さなツールウィンドウ（MFC の CMiniDockFrameWnd に相当）。
/// タイトルはペイン名、閉じるボタンはペインを隠す（<see cref="CloseRequested"/>）、
/// タイトルバーのダブルクリックで元の位置にドッキングに戻す（<see cref="RedockRequested"/>）。
/// </summary>
public sealed class FloatingPaneWindow : Form
{
    private const int WM_NCLBUTTONDBLCLK = 0x00A3;
    private const int HTCAPTION = 2;

    public FloatingPaneWindow(DockPane pane, Form owner)
    {
        Pane = pane;
        Text = pane.Title;
        Font = owner.Font;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        // オリジナル版の浮いたペインには閉じるボタンがない（compare explore-dock-float の UI ツリー）
        ControlBox = false;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Owner = owner;
    }

    public DockPane Pane { get; }

    /// <summary>タイトルバーのダブルクリック。</summary>
    public event Action<FloatingPaneWindow>? RedockRequested;

    /// <summary>閉じるボタン（Alt+F4 も）。ウィンドウは閉じずに親が隠す。</summary>
    public event Action<FloatingPaneWindow>? CloseRequested;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCLBUTTONDBLCLK && m.WParam == HTCAPTION)
        {
            RedockRequested?.Invoke(this);
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            CloseRequested?.Invoke(this);
        }
        base.OnFormClosing(e);
    }
}
