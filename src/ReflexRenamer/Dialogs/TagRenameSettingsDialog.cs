using System.Globalization;
using ReflexRenamer.Core.Rename;
using ReflexRenamer.Core.Tags;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Dialogs;

/// <summary>タグ・リネームの設定（DIALOG 172）：日時の書式と、無効な文字の置き換え。</summary>
public sealed class TagRenameSettingsDialog : TemplateForm
{
    private const int IdSample = 1044;
    private const int IdFormat = 1045;

    /// <summary>無効な文字と、その置き換え先を入れるエディットボックスの ID（テンプレートの並び順）。</summary>
    public static readonly (char Char, int Id)[] InvalidChars =
        [('\\', 1091), ('/', 1092), (':', 1093), ('*', 1094), ('?', 1095), ('"', 1096), ('<', 1097), ('>', 1098), ('|', 1099)];

    public TagRenameSettingsDialog(AppResources res, TagFormatOptions current) : base(res, 172)
    {
        var format = Get<ComboBox>(IdFormat);
        // 候補はオリジナル版のドロップダウンと同じ（explore-tagrename の datetime-formats）
        format.Items.AddRange([
            @"\Y\m\d",
            @"\y\m\d",
            @"\H\M\S",
            @"\p\I\M\S",
            @"\Y\m\d\H\M\S",
            @"\y\m\d\p\I\M\S",
            @"\Y\m\d\p\I\M\S",
            @"\y\m\d\H\M\S",
        ]);
        format.Text = current.DateTimeFormat;
        format.TextChanged += (_, _) => UpdateSample();
        foreach (var (c, id) in InvalidChars)
        {
            Get<TextBox>(id).Text = current.InvalidCharReplacements.TryGetValue(c, out var r) ? r : "";
        }
        UpdateSample();
    }

    private void UpdateSample()
    {
        var now = DateTime.Now;
        Get<Label>(IdSample).Text = $"{FileListView.FormatTime(now)} => {TagExpander.FormatTime(Get<ComboBox>(IdFormat).Text, now, CultureInfo.CurrentCulture)}";
    }

    public TagFormatOptions Options => new()
    {
        DateTimeFormat = Get<ComboBox>(IdFormat).Text,
        InvalidCharReplacements = InvalidChars.ToDictionary(x => x.Char, x => Get<TextBox>(x.Id).Text),
    };
}
