using System.Globalization;
using ReflexRenamer.Core.Rename;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Dialogs;

/// <summary>連番の設定（DIALOG 166）：開始番号、ステップ、番号の種類。</summary>
public sealed class NumberingDialog : TemplateForm
{
    public NumberingDialog(AppResources res, long start, long step, NumberType type) : base(res, 166)
    {
        Get<TextBox>(1065).Text = start.ToString(CultureInfo.InvariantCulture);
        Get<TextBox>(1067).Text = step.ToString(CultureInfo.InvariantCulture);
        Get<RadioButton>(type switch
        {
            NumberType.Hexadecimal => 1156,
            NumberType.Alphabetical => 1157,
            _ => 1155,
        }).Checked = true;
    }

    public long Start => long.TryParse(Get<TextBox>(1065).Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    public long Step => long.TryParse(Get<TextBox>(1067).Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 1;

    public NumberType Type =>
        Get<RadioButton>(1156).Checked ? NumberType.Hexadecimal
        : Get<RadioButton>(1157).Checked ? NumberType.Alphabetical
        : NumberType.Decimal;
}
