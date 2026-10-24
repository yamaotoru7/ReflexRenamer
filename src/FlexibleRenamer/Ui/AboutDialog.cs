namespace FlexibleRenamer.Ui;

/// <summary>バージョン情報（DIALOG 100）。</summary>
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
        ClientSize = new Size(DialogBuilder.X(template.Width), DialogBuilder.Y(template.Height));

        var map = res.Builder.Build(100, template, this);
        map.Get<Label>(1025).Text = "Flexible Renamer Version 8.4";
        map.Get<Label>(1027).Text = "Copyright (C) 2000-2011 Naru";
        map.Get<Label>(1028).Text = "naru@yeah.ne.jp";
        map.Get<Label>(1043).Text = res.T("http://hp.vector.co.jp/authors/VA014830/english/FlexRena/");
        if (map.TryGet(-1, out var icon) && icon is PictureBox box)
        {
            box.Image = Assets.Icon(128, 32)?.ToBitmap();
        }
        var ok = map.Get<Button>(1);
        ok.DialogResult = DialogResult.OK;
        AcceptButton = ok;
        CancelButton = ok;
    }
}
