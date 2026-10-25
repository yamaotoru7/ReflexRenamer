namespace ReflexRenamer.Ui;

/// <summary>バージョン情報（DIALOG 100）。製品名と版、Flexible Renamer にインスパイアされたという一文だけを出す。</summary>
public sealed class AboutDialog : Form
{
    public AboutDialog(AppResources res)
    {
        var template = res.Resources.Dialogs[100];
        Text = res.T(template.Title);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        // オリジナル版の下の行（Web サイト）は使わないので、その分だけ低くする
        ClientSize = new Size(DialogBuilder.X(template.Width), DialogBuilder.Y(36));

        var map = res.Builder.Build(100, template, this);
        map.Get<Label>(1025).Text = $"{AppResources.ProductName} {AppResources.DisplayVersion}";
        var credit = map.Get<Label>(1027);
        credit.Text = "Inspired by Flexible Renamer by Naru";
        credit.Width = map.Get<Label>(1043).Width;
        map.Get<Label>(1028).Visible = false;
        map.Get<Label>(1043).Visible = false;
        if (map.TryGet(-1, out var icon) && icon is PictureBox box)
        {
            box.Image = Assets.AppIcon(32)?.ToBitmap();
        }
        var ok = map.Get<Button>(1);
        ok.DialogResult = DialogResult.OK;
        AcceptButton = ok;
        CancelButton = ok;
    }
}
