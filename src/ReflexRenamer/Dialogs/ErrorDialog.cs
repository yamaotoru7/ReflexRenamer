using ReflexRenamer.Core.Rename;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Dialogs;

/// <summary>リネーム失敗時の確認（DIALOG 130）：再試行、スキップ、すべてスキップ、キャンセル。</summary>
public sealed class ErrorDialog : TemplateForm
{
    public ErrorDialog(AppResources res, string folder, string sourceName, string destinationName, string detail) : base(res, 130)
    {
        Text = AppResources.ProductName;
        var message = Get<TextBox>(1032);
        string text = Format(
            res.T("Failured to Rename!\n  Folder: %s\n  SourceName: %s\n  DestinationName: %s\n\nContinue?"),
            folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar, sourceName, destinationName).Replace("\n", "\r\n", StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(detail))
        {
            text += "\r\n\r\n" + detail.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        }
        message.Text = text;
        message.ScrollBars = ScrollBars.Vertical;
        message.WordWrap = true;
        message.TabStop = false;
        Shown += (_, _) => { message.SelectionLength = 0; Get<Button>(6).Focus(); };
        if (Map.TryGet(-1, out var icon) && icon is PictureBox box)
        {
            box.Image = SystemIcons.Error.ToBitmap();
        }
        var retry = Get<Button>(6);
        retry.DialogResult = DialogResult.Retry;
        AcceptButton = retry;
        Get<Button>(4).DialogResult = DialogResult.Ignore;
        Get<Button>(5).DialogResult = DialogResult.No;
    }

    public ErrorAction Action => DialogResult switch
    {
        DialogResult.Retry => ErrorAction.Retry,
        DialogResult.Ignore => ErrorAction.Skip,
        DialogResult.No => ErrorAction.SkipAll,
        _ => ErrorAction.Cancel,
    };
}
