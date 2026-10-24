using FlexibleRenamer.Core.Localization;
using FlexibleRenamer.Core.Resources;

namespace FlexibleRenamer.Ui;

/// <summary>
/// オリジナル版のダイアログテンプレートから WinForms のコントロールを組み立てる。
/// コントロールの Name にはリソース ID を入れる（UI Automation の AutomationId になり、自動比較で使う）。
/// 文字列は英語の原文なので、言語ファイルで訳してから設定する。
/// </summary>
public sealed class DialogBuilder(LanguageStrings strings, Font font)
{
    /// <summary>
    /// ダイアログ単位の基本値。Tahoma 9pt（96 DPI）で、オリジナル版の実測（9 DLU ＝ 16px）と一致する値。
    /// </summary>
    private const int BaseX = 7;
    private const int BaseY = 14;

    public Font Font { get; set; } = font;

    public static int X(int dlu) => (int)Math.Round(dlu * BaseX / 4.0, MidpointRounding.AwayFromZero);

    public static int Y(int dlu) => (int)Math.Round(dlu * BaseY / 8.0, MidpointRounding.AwayFromZero);

    public static Rectangle Bounds(DialogControl c) => new(X(c.X), Y(c.Y), X(c.Width), Y(c.Height));

    /// <summary>テンプレートの大きさ（ピクセル）。</summary>
    public static Size DesignSize(DialogTemplate template) => new(X(template.Width), Y(template.Height));

    /// <summary>
    /// テンプレートのコントロールを container に追加し、ID からコントロールを引ける表を返す。
    /// container はテンプレートの大きさにしてから呼ぶこと。アンカー（<see cref="Anchors"/>）を付けるので、
    /// あとで container を広げるとオリジナル版と同じように配置が変わる。
    /// </summary>
    public ControlMap Build(int dialogId, DialogTemplate template, Control container)
    {
        container.SuspendLayout();
        container.Font = Font;
        var map = new ControlMap();
        Control? previous = null;
        // ラジオボタンのグループは WS_GROUP で区切られる（WinForms は同じ親の中を全部ひとつのグループにしてしまう）
        int group = 0;
        foreach (var c in template.Controls)
        {
            if ((c.Style & 0x00020000) != 0)
            {
                group++;
            }
            var control = Create(c, previous);
            if (control is null)
            {
                continue;
            }
            if (control is RadioButton radio)
            {
                radio.AutoCheck = false;
                radio.Tag = group;
                radio.Click += (sender, _) => ((RadioButton)sender!).Checked = true;
                radio.CheckedChanged += (sender, _) =>
                {
                    var self = (RadioButton)sender!;
                    if (!self.Checked || self.Parent is null)
                    {
                        return;
                    }
                    foreach (var other in self.Parent.Controls.OfType<RadioButton>())
                    {
                        if (other != self && Equals(other.Tag, self.Tag) && other.Checked)
                        {
                            other.Checked = false;
                        }
                    }
                };
            }
            // ID が -1 の静的コントロールは AutomationId なし（オリジナル版と同じ）
            if (c.Id != -1)
            {
                control.Name = c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            control.Bounds = Bounds(c);
            control.Anchor = Anchors.For(dialogId, c.Id);
            control.Visible = c.Visible;
            control.Enabled = !c.Disabled;
            control.Font = Font;
            container.Controls.Add(control);
            if (control is GroupBox or Panel)
            {
                // グループ枠はテンプレートで先に並ぶので、そのままだと中のコントロールを隠してしまう
                control.SendToBack();
            }
            if (control is NativeUpDown updown && previous is not null)
            {
                updown.Attach(previous);
            }
            map.Add(c.Id, control);
            previous = control;
        }
        container.ResumeLayout(false);
        return map;
    }

    private Control? Create(DialogControl c, Control? previous)
    {
        string text = Translate(c.Text);
        uint style = c.Style;
        switch (c.Class)
        {
            case "Button":
            case "Button(Default)":
            case "Button(OwnerDraw)":
                return new Win32Button { Text = text, FlatStyle = FlatStyle.System, UseMnemonic = true, Multiline = (style & 0x2000) != 0 };
            case "CheckBox":
                return new Win32CheckBox { Text = text, FlatStyle = FlatStyle.System, Multiline = (style & 0x2000) != 0 };
            case "CheckBox(3state)":
                return new Win32CheckBox { Text = text, FlatStyle = FlatStyle.System, ThreeState = true, Multiline = (style & 0x2000) != 0 };
            case "RadioButton":
                return new Win32RadioButton { Text = text, FlatStyle = FlatStyle.System, AutoCheck = (style & 0xF) == 0x9, Multiline = (style & 0x2000) != 0 };
            case "GroupBox":
                return new GroupBox { Text = text, FlatStyle = FlatStyle.System };
            case "Static":
                return CreateStatic(text, style);
            case "Edit":
                return CreateEdit(style);
            case "ComboBox":
                return new ComboBox
                {
                    DropDownStyle = (style & 0x3) switch
                    {
                        1 => ComboBoxStyle.Simple,
                        3 => ComboBoxStyle.DropDownList,
                        _ => ComboBoxStyle.DropDown,
                    },
                    IntegralHeight = false,
                };
            case "ListBox":
                return new ListBox { IntegralHeight = false };
            case "SysListView32":
                return new ListView
                {
                    View = (style & 0x3) == 0x1 ? View.Details : View.List,
                    FullRowSelect = true,
                    HideSelection = false,
                    // UI Automation で Table ではなく List として見せる（オリジナル版の SysListView32 と同じ）
                    AccessibleRole = AccessibleRole.List,
                };
            case "SysTreeView32":
                return new TreeView { HideSelection = false, ShowLines = (style & 0x2) != 0, ShowPlusMinus = (style & 0x1) != 0 };
            case "SysDateTimePick32":
                return new DateTimePicker
                {
                    Format = (style & 0x9) == 0x9 ? DateTimePickerFormat.Time : DateTimePickerFormat.Long,
                    ShowCheckBox = (style & 0x2) != 0,
                    ShowUpDown = (style & 0x9) == 0x9,
                };
            case "msctls_progress32":
                return new ProgressBar();
            case "msctls_updown32":
                return previous is TextBox ? new NativeUpDown() : null;
            default:
                return null;
        }
    }

    private static Control CreateStatic(string text, uint style)
    {
        switch (style & 0x1F)
        {
            case 0x3: // SS_ICON
            case 0xE: // SS_BITMAP
                return new PictureBox { SizeMode = PictureBoxSizeMode.CenterImage, AccessibleRole = AccessibleRole.Graphic };
            case 0x10: // SS_ETCHEDHORZ
            case 0x11: // SS_ETCHEDVERT
            case 0x12: // SS_ETCHEDFRAME
                return new EtchedLine { AccessibleRole = AccessibleRole.Graphic };
            case 0x7: // SS_BLACKFRAME
                return new Panel();
        }
        // オリジナル版と同じ Win32 の STATIC で描く（WinForms の Label は SS_LEFTNOWORDWRAP でも折り返し、余白も付くので文字が欠ける）
        var label = new Win32Label
        {
            StaticStyle = style,
            Text = text,
            AutoSize = false,
            FlatStyle = FlatStyle.System,
            UseMnemonic = (style & 0x80) == 0,
            TextAlign = (style & 0x3) switch
            {
                1 => (style & 0x200) != 0 ? ContentAlignment.MiddleCenter : ContentAlignment.TopCenter,
                2 => (style & 0x200) != 0 ? ContentAlignment.MiddleRight : ContentAlignment.TopRight,
                _ => (style & 0x200) != 0 ? ContentAlignment.MiddleLeft : ContentAlignment.TopLeft,
            },
        };
        return label;
    }

    private static TextBox CreateEdit(uint style)
    {
        var box = new TextBox
        {
            Multiline = (style & 0x4) != 0,
            ReadOnly = (style & 0x800) != 0,
            TextAlign = (style & 0x3) switch
            {
                1 => HorizontalAlignment.Center,
                2 => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left,
            },
        };
        if (box.Multiline)
        {
            // UI Automation では複数行の Edit は Document（オリジナル版と同じ）
            box.AccessibleRole = AccessibleRole.Document;
        }
        if ((style & 0x2000) != 0) // ES_NUMBER
        {
            box.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsAsciiDigit(e.KeyChar);
        }
        return box;
    }

    private string Translate(string text)
    {
        // IDC_... はプログラムが実行時に書き換えるプレースホルダ、#64 などはアイコンの参照
        if (text.Length == 0 || text.StartsWith("IDC_", StringComparison.Ordinal) || text.StartsWith('#'))
        {
            return "";
        }
        // 「Target:」のようにコロンで終わるラベルは、言語ファイルにはコロンなしのキーしかない
        string translated = strings.Translate(text);
        if (translated == text && text.EndsWith(':') && text.Length > 1)
        {
            string withoutColon = strings.Translate(text[..^1]);
            if (withoutColon != text[..^1])
            {
                return withoutColon + ":";
            }
        }
        return translated;
    }
}

/// <summary>リソース ID からコントロールを引く表。同じ ID が複数あるとき（ラベルの -1 など）は最初のもの。</summary>
public sealed class ControlMap
{
    private readonly Dictionary<int, Control> _controls = [];

    public void Add(int id, Control control) => _controls.TryAdd(id, control);

    public Control this[int id] => _controls[id];

    public T Get<T>(int id) where T : Control => (T)_controls[id];

    public bool TryGet(int id, out Control control) => _controls.TryGetValue(id, out control!);
}

/// <summary>くぼんだ区切り線（SS_ETCHEDHORZ など）。</summary>
/// <summary>テンプレートの SS_* スタイル（種類・SS_NOPREFIX・SS_NOTIFY・SS_CENTERIMAGE など）をそのまま持つ Win32 の STATIC。</summary>
public sealed class Win32Label : Label
{
    public uint StaticStyle { get; set; }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (FlatStyle == FlatStyle.System)
            {
                cp.Style = (cp.Style & ~0x3FF) | (int)(StaticStyle & 0x3FF);
            }
            return cp;
        }
    }
}

public sealed class EtchedLine : Control
{
    public EtchedLine()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Height <= 2 || Width <= 2)
        {
            // 横線・縦線：上（左）が影、下（右）がハイライト
            ControlPaint.DrawBorder3D(e.Graphics, new Rectangle(0, 0, Math.Max(Width, 2), Math.Max(Height, 2)), Border3DStyle.Etched);
        }
        else
        {
            ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Etched);
        }
    }
}

/// <summary>
/// Win32 のボタンは BS_MULTILINE がなければ文字列を折り返さず1行で切る。WinForms は常に BS_MULTILINE を付けて折り返すので、
/// テンプレートに BS_MULTILINE があるときだけ折り返す（独語など長い訳語で下の行に重ならないように。T802）。
/// </summary>
public sealed class Win32Button : Button
{
    private const int BsSplitButton = 0x0C;
    private const int BsTypeMask = 0x0F;
    private const int BmSetStyle = 0xF4;
    private const int WmReflectNotify = 0x2000 + 0x004E;
    private const int BcnDropDown = -1248;

    private bool _split;

    public bool Multiline { get; set; }

    /// <summary>右端に ▼ の付いた分割ボタン（BS_SPLITBUTTON）にする。▼ を押すと <see cref="DropDown"/> が起きる。</summary>
    public bool Split
    {
        get => _split;
        set
        {
            if (_split == value)
            {
                return;
            }
            _split = value;
            if (IsHandleCreated)
            {
                int style = (int)GetWindowLongPtr(Handle, -16) & ~BsTypeMask;
                SendMessage(Handle, BmSetStyle, style | (value ? BsSplitButton : 0), 1);
                Invalidate();
            }
        }
    }

    /// <summary>分割ボタンの ▼ が押された。</summary>
    public event EventHandler? DropDown;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (!Multiline)
            {
                cp.Style &= ~0x00002000;
            }
            if (_split)
            {
                cp.Style = (cp.Style & ~BsTypeMask) | BsSplitButton;
            }
            return cp;
        }
    }

    /// <summary>メニューを出すボタンの印（右端の ▶）を描く。文字はボタン全体の中央のまま（オリジナル版の見た目）。</summary>
    public bool MenuArrow { get; set; }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x000F && MenuArrow) // WM_PAINT
        {
            base.WndProc(ref m);
            using var g = Graphics.FromHwnd(Handle);
            int cy = Height / 2;
            int x = Width - 11;
            using var brush = new SolidBrush(Enabled ? SystemColors.ControlText : SystemColors.GrayText);
            g.FillPolygon(brush, [new Point(x, cy - 4), new Point(x, cy + 4), new Point(x + 4, cy)]);
            return;
        }
        if (m.Msg == WmReflectNotify && _split)
        {
            var header = System.Runtime.InteropServices.Marshal.PtrToStructure<NmHdr>(m.LParam);
            if (header.Code == BcnDropDown)
            {
                DropDown?.Invoke(this, EventArgs.Empty);
                m.Result = 0;
                return;
            }
        }
        base.WndProc(ref m);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NmHdr
    {
        public nint HwndFrom;
        public nuint IdFrom;
        public int Code;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);
}

public sealed class Win32CheckBox : CheckBox
{
    public bool Multiline { get; set; }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (!Multiline)
            {
                cp.Style &= ~0x00002000;
            }
            return cp;
        }
    }
}

public sealed class Win32RadioButton : RadioButton
{
    public bool Multiline { get; set; }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (!Multiline)
            {
                cp.Style &= ~0x00002000;
            }
            return cp;
        }
    }
}
