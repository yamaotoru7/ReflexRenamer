namespace ReflexRenamer.Ui;

/// <summary>
/// オリジナル版のダイアログテンプレートから作るモーダルダイアログの共通部分。
/// ID 1（OK）と 2（キャンセル）のボタンは DialogResult に結び付ける。
/// </summary>
public abstract class TemplateForm : Form
{
    protected TemplateForm(AppResources res, int dialogId)
    {
        Res = res;
        var template = res.Resources.Dialogs[dialogId];
        Text = res.T(template.Title);
        Font = AppResources.UiFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        // 閉じるボタンはテンプレートに WS_SYSMENU があるときだけ（重複確認 168 やエラー 130 にはない）
        ControlBox = (template.Style & 0x00080000) != 0;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = DialogBuilder.DesignSize(template);
        Map = res.Builder.Build(dialogId, template, this);

        if (Map.TryGet(1, out var ok) && ok is Button okButton)
        {
            okButton.DialogResult = DialogResult.OK;
            AcceptButton = okButton;
        }
        if (Map.TryGet(2, out var cancel) && cancel is Button cancelButton)
        {
            cancelButton.DialogResult = DialogResult.Cancel;
            CancelButton = cancelButton;
        }
    }

    protected AppResources Res { get; }

    protected ControlMap Map { get; }

    protected T Get<T>(int id) where T : Control => Map.Get<T>(id);

    /// <summary>言語ファイルの書式文字列（<c>%s</c>）に値を当てはめる。</summary>
    protected static string Format(string format, params string[] values)
    {
        foreach (var v in values)
        {
            int i = format.IndexOf("%s", StringComparison.Ordinal);
            if (i < 0)
            {
                break;
            }
            format = format[..i] + v + format[(i + 2)..];
        }
        return format;
    }
}
