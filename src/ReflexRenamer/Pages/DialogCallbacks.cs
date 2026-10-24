using ReflexRenamer.Core.Rename;
using ReflexRenamer.Dialogs;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Pages;

/// <summary>
/// リネーム実行中の問い合わせを、オリジナル版と同じダイアログで行う。
/// 処理は別スレッドで走るので、ダイアログは UI スレッドに切り替えて出す。
/// </summary>
public sealed class DialogCallbacks(AppResources res, Control owner, ProgressDialog progress, AutoNumberSettings autoNumberDefaults) : IRenameCallbacks
{
    public ConflictDecision OnConflict(string folder, string sourceName, string destinationName) =>
        owner.Invoke(() =>
        {
            // オリジナル版は確認中に進行状況ダイアログを見せない。メインウィンドウを親にして出す
            progress.Hide();
            // 進行状況を隠すと他のアプリが前面に出ることがあるので、メインウィンドウを前に戻してから出す
            (owner as Form ?? owner.FindForm())?.Activate();
            using var dialog = new ConflictDialog(res, folder, sourceName, destinationName, autoNumberDefaults);
            dialog.ShowDialog(owner);
            progress.Show();
            return dialog.Decision;
        });

    public ErrorAction OnError(string folder, string sourceName, string destinationName, Exception error) =>
        owner.Invoke(() =>
        {
            progress.Hide();
            (owner as Form ?? owner.FindForm())?.Activate();
            using var dialog = new ErrorDialog(res, folder, sourceName, destinationName, error.Message);
            dialog.ShowDialog(owner);
            progress.Show();
            return dialog.Action;
        });

    public bool OnProgress(double ratio) => progress.Report(ratio);
}
