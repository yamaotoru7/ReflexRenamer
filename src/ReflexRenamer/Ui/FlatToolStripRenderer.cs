namespace ReflexRenamer.Ui;

/// <summary>
/// ツールバーをフラットに描くレンダラー。色はすべて SystemColors から取るので、ダークモード（Application.SetColorMode）にも追従する。
/// 既定の Professional レンダラーは Office 2007 風のグラデーションで、ダークモードでも明るいまま浮いて見える。
/// </summary>
public sealed class FlatToolStripRenderer : ToolStripProfessionalRenderer
{
    public FlatToolStripRenderer()
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(SystemColors.Control);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // 枠は描かない（下端の線もなし）
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripButton button)
        {
            base.OnRenderButtonBackground(e);
            return;
        }
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        bounds.Inflate(-1, -1);
        if (button.Checked)
        {
            // 選択中：薄い強調色の塗りと、強調色の下線
            using var fill = new SolidBrush(Color.FromArgb(40, SystemColors.Highlight));
            e.Graphics.FillRectangle(fill, bounds);
            using var line = new SolidBrush(SystemColors.Highlight);
            e.Graphics.FillRectangle(line, bounds.Left + 4, bounds.Bottom - 2, bounds.Width - 8, 2);
        }
        else if (button.Selected || button.Pressed)
        {
            using var fill = new SolidBrush(Color.FromArgb(button.Pressed ? 60 : 30, SystemColors.ControlText));
            e.Graphics.FillRectangle(fill, bounds);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        using var pen = new Pen(SystemColors.ControlDark);
        if (e.Vertical)
        {
            int x = bounds.Width / 2;
            e.Graphics.DrawLine(pen, x, 4, x, bounds.Height - 4);
        }
        else
        {
            int y = bounds.Height / 2;
            e.Graphics.DrawLine(pen, 4, y, bounds.Width - 4, y);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? SystemColors.ControlText : SystemColors.GrayText;
        base.OnRenderItemText(e);
    }
}
