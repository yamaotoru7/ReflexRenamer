using System.Globalization;
using ReflexRenamer.Core.Rename;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Dialogs;

/// <summary>重複ファイル名変更の確認（DIALOG 168）。</summary>
public sealed class ConflictDialog : TemplateForm
{
    private static readonly (int Id, ConflictAction Action)[] Radios =
    [
        (1059, ConflictAction.Retry),
        (1060, ConflictAction.Skip),
        (1061, ConflictAction.SkipAll),
        (1062, ConflictAction.Overwrite),
        (1063, ConflictAction.OverwriteAll),
        (1064, ConflictAction.AutoNumber),
    ];

    // 連番を付けるときだけ使えるコントロール。ラベル（1071、1073、1072、1081、1083）はオリジナル版では無効にならない
    private static readonly int[] AutoNumberControls = [1070, 1068, 1069, 1065, 1140, 1067, 1142];

    public ConflictDialog(AppResources res, string folder, string sourceName, string destinationName, AutoNumberSettings defaults) : base(res, 168)
    {
        Get<TextBox>(1032).Text = Format(
            res.T("Destination Name is in Collision!\n  Folder: %s\n  SourceName:  %s\n  DestinationName: %s\n\nSelect Disposal and Press Continue."),
            // フォルダは末尾に区切り文字を付けて表示する（オリジナル版と同じ）
            folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar, sourceName, destinationName).Replace("\n", "\r\n", StringComparison.Ordinal);
        if (Map.TryGet(-1, out var icon) && icon is PictureBox box)
        {
            box.Image = SystemIcons.Warning.ToBitmap();
        }

        // オリジナル版はタイトルが「Flexible Renamer」で、処理をどれも選んでいない状態で開き、選ぶまで「続行」が押せない
        Text = AppResources.ProductName;
        // 説明文は選択状態にせず、フォーカスは「キャンセル」に置く
        Get<TextBox>(1032).TabStop = false;
        Shown += (_, _) => { Get<TextBox>(1032).SelectionLength = 0; Get<Button>(2).Focus(); };
        Get<TextBox>(1070).Text = defaults.Format;
        Get<RadioButton>(defaults.AtEnd ? 1069 : 1068).Checked = true;
        Get<TextBox>(1065).Text = defaults.Start.ToString(CultureInfo.InvariantCulture);
        Get<TextBox>(1067).Text = defaults.Step.ToString(CultureInfo.InvariantCulture);

        foreach (var (id, _) in Radios)
        {
            Get<RadioButton>(id).CheckedChanged += (_, _) => UpdateEnabled();
        }
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        Get<Button>(1).Enabled = Radios.Any(r => Get<RadioButton>(r.Id).Checked);
        bool auto = Get<RadioButton>(1064).Checked;
        foreach (var id in AutoNumberControls)
        {
            if (Map.TryGet(id, out var c))
            {
                c.Enabled = auto;
            }
        }
        // ラベルはテンプレートでは無効だが、オリジナル版は有効のまま色だけ灰色にしている
        foreach (var id in new[] { 1071, 1073, 1072, 1081, 1083 })
        {
            if (Map.TryGet(id, out var label))
            {
                label.Enabled = true;
                label.ForeColor = auto ? SystemColors.ControlText : SystemColors.GrayText;
            }
        }
    }

    public ConflictDecision Decision
    {
        get
        {
            if (DialogResult != DialogResult.OK)
            {
                return new ConflictDecision(ConflictAction.Cancel);
            }
            var action = Radios.First(r => Get<RadioButton>(r.Id).Checked).Action;
            return new ConflictDecision(action, action == ConflictAction.AutoNumber ? AutoNumber : null);
        }
    }

    public AutoNumberSettings AutoNumber => new(
        Get<TextBox>(1070).Text,
        Get<RadioButton>(1069).Checked,
        long.TryParse(Get<TextBox>(1065).Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 1,
        long.TryParse(Get<TextBox>(1067).Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) ? t : 1);
}
