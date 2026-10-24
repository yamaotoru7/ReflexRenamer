namespace ReflexRenamer.Ui;

/// <summary>
/// 左側に並べるペイン（フォルダ一覧、入力フォーム）の枠。
/// 中身は Dock = Fill で直接追加する（Padding で余白を取る）。
/// 右端の数ピクセルをドラッグすると幅が変わる（<see cref="WidthRequested"/>）。
/// 実際の大きさと位置は親（MainForm）が決める。
/// </summary>
public sealed class DockPane : Panel
{
    /// <summary>幅を変えられる右端の範囲（CSizingControlBar の m_cxEdge）。</summary>
    private const int EdgeWidth = 5;

    private static readonly Padding DockedPadding = new(4);

    private bool _sizing;
    private int _sizeOffset;

    public DockPane(string title)
    {
        Title = title;
        AccessibleName = title;
        Padding = DockedPadding;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public string Title { get; }

    public bool ShowTitle { get; set; } = true;

    /// <summary>右端のドラッグで求められた幅（クランプ前）。</summary>
    public event Action<DockPane, int>? WidthRequested;

    /// <summary>右端のドラッグが終わった。</summary>
    public event Action<DockPane>? SizingFinished;

    private bool OnEdge(Point p) => p.X >= Width - EdgeWidth && p.X < Width;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_sizing && (MouseButtons & MouseButtons.Left) == 0)
        {
            // ボタンを放したのを受け取れなかった（キャプチャを失ったなど）
            _sizing = false;
            Capture = false;
            SizingFinished?.Invoke(this);
            return;
        }
        if (_sizing)
        {
            WidthRequested?.Invoke(this, e.X + _sizeOffset);
        }
        else
        {
            Cursor = OnEdge(e.Location) ? Cursors.SizeWE : Cursors.Default;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && OnEdge(e.Location))
        {
            _sizing = true;
            _sizeOffset = Width - e.X;
            Capture = true;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_sizing)
        {
            _sizing = false;
            Capture = false;
            SizingFinished?.Invoke(this);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_sizing)
        {
            Cursor = Cursors.Default;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // ペインの右端 1px に区切り線を描く
        if (Width > 0 && Height > 0)
        {
            using var pen = new Pen(SystemColors.ControlDark);
            e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height - 1);
        }
    }
}
