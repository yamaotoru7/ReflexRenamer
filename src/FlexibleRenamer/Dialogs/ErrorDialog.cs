using FlexibleRenamer.Core.Rename;
using FlexibleRenamer.Ui;

namespace FlexibleRenamer.Dialogs;

/// <summary>リネーム失敗時の確認（DIALOG 130）：再試行、スキップ、すべてスキップ、キャンセル。</summary>
public sealed class ErrorDialog : TemplateForm
{
    public ErrorDialog(AppResources res, string folder, string sourceName, string destinationName, string detail) : base(res, 130)
    {
        // オリジナル版はエラーの理由を出さず、フォルダは末尾に区切り文字を付ける（exec-error）
        _ = detail;
        Text = "Flexible Renamer";
        Get<TextBox>(1032).Text = Format(
            res.T("Failured to Rename!\n  Folder: %s\n  SourceName: %s\n  DestinationName: %s\n\nContinue?"),
            folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar, sourceName, destinationName).Replace("\n", "\r\n", StringComparison.Ordinal);
        Get<TextBox>(1032).TabStop = false;
        Shown += (_, _) => { Get<TextBox>(1032).SelectionLength = 0; Get<Button>(6).Focus(); };
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
