using System.Globalization;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Dialogs;

/// <summary>進行状況（DIALOG 133）。処理は別スレッドで走らせ、このダイアログをモーダルに出す。</summary>
public sealed class ProgressDialog : TemplateForm
{
    private volatile bool _cancelled;

    public ProgressDialog(AppResources res, string message) : base(res, 133)
    {
        Text = AppResources.ProductName;
        ControlBox = false;
        Get<Label>(1016).Text = message;
        Get<ProgressBar>(1009).Maximum = 1000;
        Get<Button>(2).Click += (_, _) => _cancelled = true;
    }

    /// <summary>処理から呼ぶ（別スレッドでもよい）。キャンセルされていたら false。</summary>
    public bool Report(double ratio)
    {
        if (_cancelled)
        {
            return false;
        }
        if (IsHandleCreated)
        {
            BeginInvoke(() =>
            {
                Get<ProgressBar>(1009).Value = (int)Math.Clamp(ratio * 1000, 0, 1000);
                Get<Label>(1013).Text = $" {(int)(ratio * 100)} %".ToString(CultureInfo.InvariantCulture);
                if (Owner is { } owner)
                {
                    TaskbarProgress.SetProgress(owner.Handle, ratio);
                }
            });
        }
        return true;
    }

    /// <summary>処理を別スレッドで実行し、終わるまでダイアログを表示する。</summary>
    public void Run(IWin32Window owner, Action work)
    {
        Exception? error = null;
        Shown += (_, _) => Task.Run(() =>
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                BeginInvoke(() => { DialogResult = DialogResult.OK; Close(); });
            }
        });
        ShowDialog(owner);
        if (owner is Form form)
        {
            TaskbarProgress.Clear(form.Handle);
        }
        if (error is not null)
        {
            throw new InvalidOperationException(error.Message, error);
        }
    }
}
