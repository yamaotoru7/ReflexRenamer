using System.Runtime.InteropServices;

namespace FlexibleRenamer.Ui;

/// <summary>
/// Win32 のスピンボタン（msctls_updown32）そのもの。オリジナル版と同じく、隣のエディットボックスを
/// バディにして右端に食い込ませ（UDS_ALIGNRIGHT）、値を整数として増減する（UDS_SETBUDDYINT）。
/// </summary>
public sealed class NativeUpDown : Control
{
    private const int UdsSetBuddyInt = 0x0002;
    private const int UdsAlignRight = 0x0004;
    private const int UdsArrowKeys = 0x0020;
    private const int UdsNoThousands = 0x0080;
    private const int UdmSetRange32 = 0x0400 + 111;
    private const int UdmSetBuddy = 0x0400 + 105;

    private Control? _buddy;

    public NativeUpDown()
    {
        SetStyle(ControlStyles.Selectable, false);
        // 描画はコモンコントロールに任せる（UserPaint のままだと WinForms が背景で塗りつぶし、ボタンが見えない）
        SetStyle(ControlStyles.UserPaint, false);
        TabStop = false;
    }

    public int Minimum { get; set; }

    public int Maximum { get; set; } = 9999;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassName = "msctls_updown32";
            // UDS_ALIGNRIGHT はコモンコントロールがバディを縮めるが、その後 WinForms がバディの幅を戻してスピンボタンを覆ってしまう。
            // 配置は Arrange で同じ規則（バディを縮め、右端に 2px 重ねて置く）で行う
            cp.Style |= UdsSetBuddyInt | UdsArrowKeys | UdsNoThousands;
            return cp;
        }
    }

    /// <summary>バディを設定する。位置はコモンコントロールがバディに合わせて決める。</summary>
    public void Attach(Control buddy)
    {
        _buddy = buddy;
        _buddyWidth = buddy.Width;
        if (IsHandleCreated)
        {
            SetBuddy();
        }
        buddy.VisibleChanged += (_, _) => Visible = buddy.Visible;
        buddy.EnabledChanged += (_, _) => Enabled = buddy.Enabled;
        buddy.LocationChanged += (_, _) => Arrange();
        buddy.SizeChanged += (_, _) => Arrange();
        Arrange();
    }

    /// <summary>バディのテンプレート上の幅（スピンボタンの分を含む）。</summary>
    private int _buddyWidth;
    private bool _arranging;

    /// <summary>UDS_ALIGNRIGHT と同じ配置：バディをスピンボタンの幅だけ縮め（2px は重ねる）、その右端にバディと同じ高さで置く。</summary>
    private void Arrange()
    {
        if (_buddy is null || _arranging)
        {
            return;
        }
        _arranging = true;
        try
        {
            int width = Width;
            _buddy.Width = Math.Max(1, _buddyWidth - width + 2);
            Bounds = new Rectangle(_buddy.Right - 2, _buddy.Top, width, _buddy.Height);
            BringToFront();
        }
        finally
        {
            _arranging = false;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SendMessage(Handle, UdmSetRange32, Minimum, Maximum);
        if (_buddy is not null)
        {
            SetBuddy();
        }
    }

    private void SetBuddy()
    {
        if (_buddy is not null)
        {
            SendMessage(Handle, UdmSetBuddy, _buddy.Handle, 0);
        }
    }

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint hwnd, int msg, nint wParam, nint lParam);
}
