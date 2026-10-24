namespace FlexibleRenamer.Ui;

/// <summary>
/// オリジナル版の MFC コントロールバー（ドッキングペイン）に似せた枠。上端につまみ（二重線）を描く。
/// 中身は Dock = Fill で直接追加する（Padding で位置を決める）。
/// <para>
/// オリジナル版の CSizingControlBar と同じく、右端の数ピクセルをドラッグすると幅が変わる（<see cref="WidthRequested"/>）。
/// つまみをドラッグすると枠を描いて動かし、放した位置を <see cref="MoveRequested"/> で知らせる。
/// つまみのダブルクリックは <see cref="FloatRequested"/>（MFC の CControlBar と同じく浮かせる）。
/// 浮かせている間（<see cref="Floating"/>）はつまみと右端の幅変更をなくし、ツールウィンドウの枠で大きさを変える。
/// 実際の大きさと位置は親（MainForm）が決める。
/// </para>
/// </summary>
public sealed class DockPane : Panel
{
    /// <summary>つまみの高さ。オリジナル版では中身が上端から 18px 下がる。</summary>
    private const int GripperHeight = 11;

    /// <summary>幅を変えられる右端の範囲（CSizingControlBar の m_cxEdge）。</summary>
    private const int EdgeWidth = 5;

    /// <summary>つまみのドラッグを始めるまでの移動量。</summary>
    private static readonly Size DragThreshold = SystemInformation.DragSize;

    private enum DragMode { None, Size, MovePending, Move }

    private DragMode _drag;
    private Point _dragStart;
    private int _sizeOffset;
    private Rectangle? _frame;
    private bool _floating;

    /// <summary>ドッキングしているときの余白（オリジナル版の実測：左 6、上 18、右 8、下 7）。</summary>
    private static readonly Padding DockedPadding = new(6, GripperHeight + 7, 8, 7);

    /// <summary>浮かせているときの余白（つまみがないので上も詰める）。</summary>
    private static readonly Padding FloatingPadding = new(4, 4, 4, 4);

    public DockPane(string title)
    {
        Title = title;
        AccessibleName = title;
        Padding = DockedPadding;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public string Title { get; }

    public bool ShowTitle { get; set; } = true;

    /// <summary>浮かせたウィンドウに入っている。</summary>
    public bool Floating
    {
        get => _floating;
        set
        {
            if (_floating != value)
            {
                _floating = value;
                Padding = value ? FloatingPadding : DockedPadding;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }
    }

    /// <summary>つまみのダブルクリック。</summary>
    public event Action<DockPane>? FloatRequested;

    /// <summary>右端のドラッグで求められた幅（クランプ前）。</summary>
    public event Action<DockPane, int>? WidthRequested;

    /// <summary>右端のドラッグが終わった。</summary>
    public event Action<DockPane>? SizingFinished;

    /// <summary>つまみのドラッグを放した。マウスの位置と、ドラッグ中に描いていた枠（どちらもスクリーン座標）。</summary>
    public event Action<DockPane, Point, Rectangle>? MoveRequested;

    private bool OnEdge(Point p) => !_floating && p.X >= Width - EdgeWidth && p.X < Width;

    private bool OnGripper(Point p) => !_floating && p.Y < Padding.Top && p.X < Width - EdgeWidth;

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && OnGripper(e.Location))
        {
            EndDrag();
            FloatRequested?.Invoke(this);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drag != DragMode.None && (MouseButtons & MouseButtons.Left) == 0)
        {
            // ボタンを放したのを受け取れなかった（キャプチャを失ったなど）
            var mode = _drag;
            EndDrag();
            if (mode == DragMode.Size)
            {
                SizingFinished?.Invoke(this);
            }
            return;
        }
        switch (_drag)
        {
            case DragMode.Size:
                WidthRequested?.Invoke(this, e.X + _sizeOffset);
                break;
            case DragMode.MovePending:
                if (Math.Abs(e.X - _dragStart.X) > DragThreshold.Width || Math.Abs(e.Y - _dragStart.Y) > DragThreshold.Height)
                {
                    _drag = DragMode.Move;
                    DrawFrame(e.Location);
                }
                break;
            case DragMode.Move:
                DrawFrame(e.Location);
                break;
            default:
                Cursor = OnEdge(e.Location) ? Cursors.SizeWE : Cursors.Default;
                break;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        if (OnEdge(e.Location))
        {
            _drag = DragMode.Size;
            _sizeOffset = Width - e.X;
            Capture = true;
        }
        else if (OnGripper(e.Location))
        {
            _drag = DragMode.MovePending;
            _dragStart = e.Location;
            Capture = true;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var mode = _drag;
        var frame = _frame ?? RectangleToScreen(ClientRectangle);
        EndDrag();
        if (mode == DragMode.Size)
        {
            SizingFinished?.Invoke(this);
        }
        else if (mode == DragMode.Move)
        {
            MoveRequested?.Invoke(this, PointToScreen(e.Location), frame);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_drag == DragMode.None)
        {
            Cursor = Cursors.Default;
        }
    }

    private void EndDrag()
    {
        EraseFrame();
        _drag = DragMode.None;
        Capture = false;
    }

    /// <summary>ドラッグ中のペインの枠（MFC のドラッグ枠と同じく反転で描く）。</summary>
    private void DrawFrame(Point location)
    {
        EraseFrame();
        var origin = PointToScreen(new Point(location.X - _dragStart.X, location.Y - _dragStart.Y));
        var rect = new Rectangle(origin, Size);
        ControlPaint.DrawReversibleFrame(rect, SystemColors.Control, FrameStyle.Thick);
        _frame = rect;
    }

    private void EraseFrame()
    {
        if (_frame is { } old)
        {
            ControlPaint.DrawReversibleFrame(old, SystemColors.Control, FrameStyle.Thick);
            _frame = null;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_floating)
        {
            return;
        }
        // つまみ：横向きの二重のくぼみ線
        int left = 4;
        int right = Width - 4;
        for (int y = 6; y <= 9; y += 3)
        {
            e.Graphics.DrawLine(SystemPens.ControlLightLight, left, y, right, y);
            e.Graphics.DrawLine(SystemPens.ControlDark, left, y + 1, right, y + 1);
        }
    }
}
