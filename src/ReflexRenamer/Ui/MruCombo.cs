using ReflexRenamer.Core.Settings;

namespace ReflexRenamer.Ui;

/// <summary>
/// 入力欄の履歴（コンボボックスのドロップダウン）。設定ファイルのセクション（<c>[WildCardSearch]</c> など）の MRU を出し入れする。
/// </summary>
public static class MruCombo
{
    /// <summary>設定から履歴を読んでドロップダウンに入れる。</summary>
    public static void Load(ComboBox combo, AppSettings settings, string section)
    {
        combo.Items.Clear();
        foreach (var v in settings.GetMru(section))
        {
            combo.Items.Add(v);
        }
    }

    /// <summary>今の文字列を履歴の先頭に入れ、ドロップダウンも更新する。</summary>
    public static void Commit(ComboBox combo, AppSettings settings, string section)
    {
        string text = combo.Text;
        if (text.Length == 0)
        {
            return;
        }
        settings.PushMru(section, text);
        Load(combo, settings, section);
        combo.Text = text;
    }
}
