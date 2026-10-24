namespace ReflexRenamer.Ui;

/// <summary>
/// 4ページの切り替えタブ（リネーム、タグ・リネーム、属性、連番オブジェクト）。
/// 自動操作（FrTools）が <c>type:CheckBox,name:TagRename</c> で探してクリックするため、
/// 各タブは <see cref="AccessibleRole.CheckButton"/> の子コントロールとして並べる。
/// </summary>
public sealed class PageTabStrip : Control
{
    private readonly Action<int> _onCommand;
    private readonly List<TabItem> _items = [];
    private int _selectedCommand = Commands.PageRename;

    public PageTabStrip(AppResources res, Action<int> onCommand)
    {
        _onCommand = onCommand;
        Dock = DockStyle.Top;
        BackColor = SystemColors.Control;
        TabStop = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        AddTab(Commands.PageRename, res.T("Rename"), "Rename", FluentGlyphs.Rename);
        AddTab(Commands.PageTagRename, res.T("TagRename"), "TagRename", FluentGlyphs.Tag);
        AddTab(Commands.PageAttribute, res.T("Attribute"), "Modify attributes", FluentGlyphs.Attribute);
        AddTab(Commands.PageNumberedObject, res.T("NumberedObject"), "Create numbered object", FluentGlyphs.NumberedObject);

        LayoutTabs();
    }

    /// <summary>現在選択されているページのコマンド ID。</summary>
    public int SelectedCommand
    {
        get => _selectedCommand;
        set
        {
            _selectedCommand = value;
            foreach (var item in _items)
            {
                item.Selected = item.Command == value;
            }
        }
    }

    private void AddTab(int command, string label, string accessibleName, char glyph)
    {
        var item = new TabItem(command, label, accessibleName, glyph, () => _onCommand(command))
        {
            Selected = command == _selectedCommand,
        };
        _items.Add(item);
        Controls.Add(item);
    }

    private void LayoutTabs()
    {
        int height = Font.Height + 16;
        if (Height != height)
        {
            Height = height;
        }
        int x = 6;
        foreach (var item in _items)
        {
            int w = item.PreferredWidth(Font);
            item.Bounds = new Rectangle(x, 0, w, height);
            x += w;
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        LayoutTabs();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutTabs();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int index = _items.FindIndex(i => i.Command == _selectedCommand);
        if (index < 0)
        {
            return;
        }
        if (e.KeyCode == Keys.Left && index > 0)
        {
            _onCommand(_items[index - 1].Command);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right && index < _items.Count - 1)
        {
            _onCommand(_items[index + 1].Command);
            e.Handled = true;
        }
    }

    private sealed class TabItem : Control
    {
        private const int GlyphSize = 16;
        private const int LeftPadding = 10;
        private const int GlyphGap = 6;
        private const int RightPadding = 12;

        private readonly Action _onClick;
        private bool _selected;
        private bool _hovered;

        public TabItem(int command, string label, string accessibleName, char glyph, Action onClick)
        {
            Command = command;
            Label = label;
            Glyph = glyph;
            _onClick = onClick;

            Name = accessibleName;
            AccessibleName = accessibleName;
            AccessibleRole = AccessibleRole.CheckButton;
            Text = label;
            BackColor = SystemColors.Control;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        public int Command { get; }

        public string Label { get; }

        public char Glyph { get; }

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    Invalidate();
                }
            }
        }

        public int PreferredWidth(Font font)
        {
            int textWidth = TextRenderer.MeasureText(
                Label,
                font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
            return LeftPadding + GlyphSize + GlyphGap + textWidth + RightPadding;
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new TabAccessibleObject(this);

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
            {
                _onClick();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);

            Color fore = _selected || _hovered ? SystemColors.ControlText : SystemColors.GrayText;
            int glyphY = (Height - GlyphSize) / 2;
            FluentGlyphs.Draw(e.Graphics, Glyph, new Rectangle(LeftPadding, glyphY, GlyphSize, GlyphSize), fore);

            int textX = LeftPadding + GlyphSize + GlyphGap;
            var textRect = new Rectangle(textX, 0, Math.Max(Width - textX - RightPadding, 0), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Label,
                Font,
                textRect,
                fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            if (_selected)
            {
                using var brush = new SolidBrush(SystemColors.Highlight);
                e.Graphics.FillRectangle(brush, 0, Height - 2, Width, 2);
            }
        }

        private sealed class TabAccessibleObject(TabItem owner) : ControlAccessibleObject(owner)
        {
            public override string? Name => owner.AccessibleName;

            public override AccessibleRole Role => AccessibleRole.CheckButton;

            public override AccessibleStates State =>
                base.State | (owner.Selected ? AccessibleStates.Checked : AccessibleStates.None);

            public override void DoDefaultAction() => owner._onClick();
        }
    }
}
