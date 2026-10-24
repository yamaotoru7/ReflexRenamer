using FlexibleRenamer.Ui;

namespace FlexibleRenamer.Dialogs;

/// <summary>1行の文字列を聞く小さなダイアログ（プリセットの名前など）。オリジナル版にテンプレートがないので手で組む。</summary>
public sealed class InputDialog : Form
{
    private readonly TextBox _text = new();

    public InputDialog(AppResources res, string title, string prompt, string initial)
    {
        Text = title;
        Font = AppResources.UiFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(DialogBuilder.X(200), DialogBuilder.Y(56));

        var label = new Label { Text = prompt, AutoSize = false, Bounds = new Rectangle(DialogBuilder.X(7), DialogBuilder.Y(7), DialogBuilder.X(186), DialogBuilder.Y(9)) };
        _text.Bounds = new Rectangle(DialogBuilder.X(7), DialogBuilder.Y(18), DialogBuilder.X(186), DialogBuilder.Y(12));
        _text.Text = initial;
        var ok = new Button { Text = res.T("OK"), DialogResult = DialogResult.OK, Bounds = new Rectangle(DialogBuilder.X(89), DialogBuilder.Y(36), DialogBuilder.X(50), DialogBuilder.Y(14)), FlatStyle = FlatStyle.System };
        var cancel = new Button { Text = res.T("Cancel"), DialogResult = DialogResult.Cancel, Bounds = new Rectangle(DialogBuilder.X(143), DialogBuilder.Y(36), DialogBuilder.X(50), DialogBuilder.Y(14)), FlatStyle = FlatStyle.System };
        Controls.AddRange([label, _text, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public string Value => _text.Text;

    public static string? Show(IWin32Window owner, AppResources res, string title, string prompt, string initial = "")
    {
        using var dialog = new InputDialog(res, title, prompt, initial);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Value : null;
    }
}
