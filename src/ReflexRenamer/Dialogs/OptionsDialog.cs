using System.Globalization;
using ReflexRenamer.Core.Rename;
using ReflexRenamer.Core.Resources;
using ReflexRenamer.Core.Settings;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Dialogs;

/// <summary>
/// オプション（ツール > オプション）。オリジナル版はツリー形式のプロパティシート（explore-options の実測）：
/// 左にページのツリー（全般、リネーム）、右にページ（DIALOG 237 / 238）を収めた枠、下に OK とキャンセル。
/// </summary>
public sealed class OptionsDialog : Form
{
    // DIALOG 237（全般）
    private const int IdContextMenu = 1149;
    private const int IdSendToList = 1150;
    private const int IdGeneralIcon = 1178;

    // DIALOG 238（リネーム）
    private const int IdStrFunc = 1074;
    private const int IdAskEveryTime = 1063;
    private const int IdAutoNumber = 1064;
    private const int IdFormat = 1070;
    private const int IdAtStart = 1068;
    private const int IdAtEnd = 1069;
    private const int IdStart = 1065;
    private const int IdStep = 1067;
    private const int IdRenameIcon = 1179;

    // 連番の設定の入力欄。ラベル（1071、1073、1072、1081、1083）はオリジナル版では無効にならない（explore-options）
    private static readonly int[] AutoNumberControls = [1070, 1068, 1069, 1065, 1140, 1067, 1142];
    private static readonly int[] AutoNumberLabels = [1071, 1073, 1072, 1081, 1083];

    // 実測の配置（クライアント座標）。ツリー (7,8) 145x377、ページの枠 (157,8) 446x377、ページは枠の中の (22,51) から
    private static readonly Rectangle TreeBounds = new(7, 8, 145, 377);
    private static readonly Rectangle FrameBounds = new(157, 8, 446, 377);
    private static readonly Point PageOffset = new(22, 51);

    private readonly AppResources _res;
    private readonly ControlMap _general;
    private readonly ControlMap _rename;
    private readonly TreeView _tree;
    private readonly Label _caption;
    private readonly PictureBox _headerIcon;
    private readonly string[] _descriptions;
    private readonly Image?[] _icons;
    private readonly Panel[] _pages;

    public OptionsDialog(AppResources res)
    {
        _res = res;
        // タイトルは「オプション」（メニューの訳からアクセスキーと「...」を除く）
        Text = System.Text.RegularExpressions.Regex.Replace(res.T("&Options...").Replace("&", "").TrimEnd('.'), @"\([A-Za-z]\)$", "");
        Font = AppResources.UiFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(610, 426);

        var generalTemplate = res.Resources.Dialogs[237];
        var renameTemplate = res.Resources.Dialogs[238];

        _tree = new TreeView { Bounds = TreeBounds, HideSelection = false, ShowLines = false, ShowPlusMinus = false, ShowRootLines = false, FullRowSelect = true, Name = "32494" };
        _tree.Nodes.Add(res.T(generalTemplate.Title));
        _tree.Nodes.Add(res.T(renameTemplate.Title));

        // 枠の上部はページのアイコンと説明（「全般的なオプションを変更します」など）の帯、その下に区切り線（実測の見た目）
        var frame = new Panel { Bounds = FrameBounds, BorderStyle = BorderStyle.FixedSingle, Name = "65535", AccessibleName = "PropPageFrameBordered" };
        _headerIcon = new PictureBox { Bounds = new Rectangle(12, 8, 32, 32), SizeMode = PictureBoxSizeMode.CenterImage, AccessibleRole = AccessibleRole.Graphic };
        _caption = new Label { Bounds = new Rectangle(56, 8, FrameBounds.Width - 70, 32), TextAlign = ContentAlignment.MiddleLeft };
        var separator = new EtchedLine { Bounds = new Rectangle(8, 46, FrameBounds.Width - 18, 2), AccessibleRole = AccessibleRole.Graphic };
        frame.Controls.AddRange([_headerIcon, _caption, separator]);

        var generalPage = NewPage(generalTemplate);
        var renamePage = NewPage(renameTemplate);
        _pages = [generalPage, renamePage];
        _general = res.Builder.Build(237, generalTemplate, generalPage);
        _rename = res.Builder.Build(238, renameTemplate, renamePage);
        frame.Controls.AddRange(_pages);
        foreach (var page in _pages)
        {
            page.Location = PageOffset;
        }

        var ok = new Button { Text = res.T("OK"), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.System, Bounds = new Rectangle(421, 392, 88, 26), Name = "1" };
        var cancel = new Button { Text = res.T("Cancel"), DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.System, Bounds = new Rectangle(516, 392, 88, 26), Name = "2" };
        Controls.AddRange([_tree, frame, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;

        if (_general.TryGet(IdGeneralIcon, out var gi) && gi is PictureBox gBox) gBox.Image = Assets.AppIcon(16)?.ToBitmap();
        if (_rename.TryGet(IdRenameIcon, out var ri) && ri is PictureBox rBox) rBox.Image = Assets.Icon(Ui.Commands.PageRename, 16)?.ToBitmap();

        // 全般：フォルダのコンテキストメニュー、「送る」メニュー（ページごとに3つ）
        _general.Get<CheckBox>(IdContextMenu).Checked = ShellIntegration.IsContextMenuRegistered();
        var sendTo = _general.Get<ListView>(IdSendToList);
        sendTo.CheckBoxes = true;
        sendTo.HeaderStyle = ColumnHeaderStyle.None;
        sendTo.Columns.Add("", sendTo.ClientSize.Width - 4);
        var sendToIcons = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
        sendTo.SmallImageList = sendToIcons;
        foreach (var (page, title) in ShellIntegration.SendToPages(res))
        {
            string key = page.ToString(CultureInfo.InvariantCulture);
            if (Assets.Icon(page, 16) is { } icon)
            {
                sendToIcons.Images.Add(key, icon);
            }
            sendTo.Items.Add(new ListViewItem(title, key) { Tag = page, Checked = ShellIntegration.IsSendToRegistered(page) });
        }

        // リネーム：StrFunc.dll（内蔵関数で代替済み）、重複時の処理
        var strFunc = _rename.Get<CheckBox>(IdStrFunc);
        strFunc.Enabled = false;
        strFunc.Text += $" ({res.T("built in")})";
        var s = res.Settings;
        // オリジナル版の ini のキーは DuplicateIndex（1 なら「以下の設定で連番を付ける」、それ以外は毎回選択。作者の ini には 2 があるが意味は未確認）
        bool auto = s.GetInt("Rename", "DuplicateIndex", 0) == 1;
        _rename.Get<RadioButton>(auto ? IdAutoNumber : IdAskEveryTime).Checked = true;
        var defaults = ConflictDefaults(s);
        _rename.Get<TextBox>(IdFormat).Text = defaults.Format;
        _rename.Get<RadioButton>(defaults.AtEnd ? IdAtEnd : IdAtStart).Checked = true;
        _rename.Get<TextBox>(IdStart).Text = defaults.Start.ToString(CultureInfo.InvariantCulture);
        _rename.Get<TextBox>(IdStep).Text = defaults.Step.ToString(CultureInfo.InvariantCulture);
        foreach (var id in new[] { IdAskEveryTime, IdAutoNumber })
        {
            _rename.Get<RadioButton>(id).CheckedChanged += (_, _) => UpdateEnabled();
        }
        UpdateEnabled();

        _descriptions = [res.T("Modify the general option"), res.T("Modify the rename option")];
        _icons = [Assets.AppIcon(32)?.ToBitmap(), Assets.Icon(Ui.Commands.PageRename, 32)?.ToBitmap()];
        _tree.AfterSelect += (_, _) => ShowPage(_tree.SelectedNode?.Index ?? 0);
        _tree.SelectedNode = _tree.Nodes[0];
        ShowPage(0);
    }

    private static Panel NewPage(DialogTemplate template) =>
        new() { Size = DialogBuilder.DesignSize(template), Visible = false };

    private void ShowPage(int index)
    {
        for (int i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visible = i == index;
        }
        _caption.Text = _descriptions[index];
        _headerIcon.Image = _icons[index];
    }

    private void UpdateEnabled()
    {
        bool auto = _rename.Get<RadioButton>(IdAutoNumber).Checked;
        foreach (var id in AutoNumberControls)
        {
            if (_rename.TryGet(id, out var c))
            {
                c.Enabled = auto;
            }
        }
        foreach (var id in AutoNumberLabels)
        {
            if (_rename.TryGet(id, out var label))
            {
                label.Enabled = true;
                label.ForeColor = auto ? SystemColors.ControlText : SystemColors.GrayText;
            }
        }
    }

    /// <summary>設定を保存し、シェルへの登録を反映する。</summary>
    public void Apply()
    {
        var s = _res.Settings;
        s.Set("Rename", "DuplicateIndex", _rename.Get<RadioButton>(IdAutoNumber).Checked ? 1 : 0);
        s.Set("Rename", "DuplicateFormat", _rename.Get<TextBox>(IdFormat).Text);
        s.Set("Rename", "DuplicateSuffix", _rename.Get<RadioButton>(IdAtEnd).Checked ? 1 : 0);
        s.Set("Rename", "DuplicateNumberingBase", _rename.Get<TextBox>(IdStart).Text);
        s.Set("Rename", "DuplicateNumberingStep", _rename.Get<TextBox>(IdStep).Text);
        ShellIntegration.SetContextMenu(_general.Get<CheckBox>(IdContextMenu).Checked);
        foreach (ListViewItem item in _general.Get<ListView>(IdSendToList).Items)
        {
            ShellIntegration.SetSendTo((int)item.Tag!, item.Text, item.Checked);
        }
    }

    /// <summary>重複時の連番の既定値（[Rename] の Duplicate*）。</summary>
    public static AutoNumberSettings ConflictDefaults(AppSettings s) => new(
        s.Get("Rename", "DuplicateFormat", "(?)"),  // 既定は「(?)」（重複確認ダイアログの実測。例の表示は「(??)」）
        s.GetInt("Rename", "DuplicateSuffix", 1) != 0,
        s.GetInt("Rename", "DuplicateNumberingBase", 1),
        s.GetInt("Rename", "DuplicateNumberingStep", 1));
}

/// <summary>シェルへの登録：フォルダの右クリックメニューと「送る」メニュー（現在のユーザーだけ）。</summary>
public static class ShellIntegration
{
    private const string ContextMenuKey = @"Software\Classes\Folder\shell\Reflex Renamer";

    private static readonly (int Command, string Name)[] Pages =
    [
        (Commands.PageRename, "Rename"),
        (Commands.PageTagRename, "TagRename"),
        (Commands.PageAttribute, "Attribute"),
    ];

    private static string ExePath => Environment.ProcessPath ?? Application.ExecutablePath;

    /// <summary>「送る」の項目：ページのコマンド ID と表示名「Reflex Renamer (リネーム) 」（オリジナル版と同じく末尾に空白）。</summary>
    public static IEnumerable<(int Page, string Title)> SendToPages(AppResources res) =>
        Pages.Select(p => (p.Command, $"Reflex Renamer ({res.T(p.Name)}) "));

    private static string ArgumentOf(int page) => Pages.First(p => p.Command == page).Name;

    private static string SendToLink(string title) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SendTo), title.TrimEnd() + ".lnk");

    public static bool IsContextMenuRegistered()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ContextMenuKey + @"\command");
        return key?.GetValue("") is string;
    }

    public static void SetContextMenu(bool enabled)
    {
        try
        {
            if (enabled)
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(ContextMenuKey);
                key.SetValue("", "Reflex Renamer");
                key.SetValue("Icon", $"\"{ExePath}\",0");
                using var command = key.CreateSubKey("command");
                command.SetValue("", $"\"{ExePath}\" \"%1\"");
            }
            else
            {
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(ContextMenuKey, throwOnMissingSubKey: false);
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
        }
    }

    public static bool IsSendToRegistered(int page) => IsSendToRegistered(ArgumentOf(page));

    public static bool IsSendToRegistered(string page)
    {
        string dir = Environment.GetFolderPath(Environment.SpecialFolder.SendTo);
        return Directory.Exists(dir) && Directory.EnumerateFiles(dir, "Reflex Renamer*.lnk")
            .Any(f => ReadLinkArguments(f)?.Contains("/" + page, StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>ショートカットを作る（引数 <c>/Rename</c> などで起動するページを指定する）か消す。</summary>
    public static void SetSendTo(int page, string title, bool enabled) => SetSendTo(ArgumentOf(page), title, enabled);

    public static void SetSendTo(string page, string title, bool enabled)
    {
        try
        {
            string path = SendToLink(title);
            if (enabled)
            {
                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type is null)
                {
                    return;
                }
                dynamic shell = Activator.CreateInstance(type)!;
                dynamic link = shell.CreateShortcut(path);
                link.TargetPath = ExePath;
                link.Arguments = "/" + page;
                link.Save();
            }
            else
            {
                File.Delete(path);
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Runtime.InteropServices.COMException)
        {
        }
    }

    private static string? ReadLinkArguments(string path)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null)
            {
                return null;
            }
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic link = shell.CreateShortcut(path);
            return (string?)link.Arguments;
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or IOException)
        {
            return null;
        }
    }
}
