using System.Globalization;
using ReflexRenamer.Core.NumberedObjects;
using ReflexRenamer.Dialogs;
using ReflexRenamer.Ui;

namespace ReflexRenamer.Pages;

/// <summary>連番オブジェクトページ（DIALOG 142）。</summary>
public sealed class NumberedObjectPage : PageBase
{
    private const int IdEmptyFile = 1077;
    private const int IdCopiedFile = 1078;
    private const int IdEmptyFolder = 1079;
    private const int IdCopiedFolder = 1080;
    private const int IdSize = 1002;
    private const int IdSourceFile = 1058;
    private const int IdBrowseFile = 1073;
    private const int IdSourceFolder = 1059;
    private const int IdBrowseFolder = 1074;
    private const int IdName = 1018;
    private const int IdAmount = 1022;
    private const int IdStart = 1065;
    private const int IdStep = 1067;
    private const int IdCreate = 1011;

    private readonly ControlMap _map;
    private bool _updating;

    public NumberedObjectPage(AppResources res, MainForm main) : base(res, main)
    {
        var template = res.Resources.Dialogs[142];
        Size = DialogBuilder.DesignSize(template);
        _map = res.Builder.Build(142, template, this);
        Tooltips.Apply(142, _map, res, new ToolTip());
        _map[1000].Visible = false;
        _map.Get<PictureBox>(1009).Image = Assets.Icon(Ui.Commands.PageNumberedObject, 32)?.ToBitmap();

        _updating = true;
        Get<RadioButton>(IdEmptyFile).Checked = true;
        Get<TextBox>(IdSize).Text = "0";
        Get<TextBox>(IdAmount).Text = "10";
        Get<TextBox>(IdStart).Text = "0";
        Get<TextBox>(IdStep).Text = "1";
        var name = Get<TextBox>(IdName);
        name.PlaceholderText = "<" + res.T("Enter Name Here") + ">";
        name.GotFocus += (_, _) => name.SelectAll();
        _updating = false;

        foreach (var id in new[] { IdEmptyFile, IdCopiedFile, IdEmptyFolder, IdCopiedFolder })
        {
            Get<RadioButton>(id).CheckedChanged += (_, _) => { UpdateEnabled(); UpdatePreview(); };
        }
        foreach (var id in new[] { IdName, IdAmount, IdStart, IdStep, IdSize })
        {
            Get<TextBox>(id).TextChanged += (_, _) => UpdatePreview();
        }
        Get<Button>(IdBrowseFile).Click += (_, _) => Browse(IdSourceFile, folder: false);
        Get<Button>(IdBrowseFolder).Click += (_, _) => Browse(IdSourceFolder, folder: true);
        Get<Button>(IdCreate).Click += (_, _) => Create();
        Main.List.ClientSizeChanged += OnListClientSizeChanged;
        UpdateEnabled();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Main.List.ClientSizeChanged -= OnListClientSizeChanged;
        }
        base.Dispose(disposing);
    }

    private T Get<T>(int id) where T : Control => _map.Get<T>(id);

    private NumberedObjectKind Kind =>
        Get<RadioButton>(IdCopiedFile).Checked ? NumberedObjectKind.CopiedFile
        : Get<RadioButton>(IdEmptyFolder).Checked ? NumberedObjectKind.EmptyFolder
        : Get<RadioButton>(IdCopiedFolder).Checked ? NumberedObjectKind.CopiedFolder
        : NumberedObjectKind.EmptyFile;

    private void UpdateEnabled()
    {
        var kind = Kind;
        Get<TextBox>(IdSize).Enabled = kind == NumberedObjectKind.EmptyFile;
        _map[1054].Enabled = kind == NumberedObjectKind.EmptyFile;
        Get<TextBox>(IdSourceFile).Enabled = kind == NumberedObjectKind.CopiedFile;
        Get<Button>(IdBrowseFile).Enabled = kind == NumberedObjectKind.CopiedFile;
        Get<TextBox>(IdSourceFolder).Enabled = kind == NumberedObjectKind.CopiedFolder;
        Get<Button>(IdBrowseFolder).Enabled = kind == NumberedObjectKind.CopiedFolder;
    }

    private void Browse(int textId, bool folder)
    {
        if (folder)
        {
            using var dialog = new FolderBrowserDialog { Description = Res.T("Select Source Folder for Copy") };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Get<TextBox>(textId).Text = dialog.SelectedPath;
            }
        }
        else
        {
            using var dialog = new OpenFileDialog { Title = Res.T("Select Source File for Copy") };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Get<TextBox>(textId).Text = dialog.FileName;
            }
        }
    }

    private NumberedObjectOptions Options()
    {
        static long Long(string s, long fallback) => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        return new NumberedObjectOptions
        {
            Kind = Kind,
            Size = Get<TextBox>(IdSize).Text,
            Source = Kind == NumberedObjectKind.CopiedFile ? Get<TextBox>(IdSourceFile).Text : Get<TextBox>(IdSourceFolder).Text,
            Name = Get<TextBox>(IdName).Text,
            Amount = (int)Math.Clamp(Long(Get<TextBox>(IdAmount).Text, 0), 0, 100000),
            Start = Long(Get<TextBox>(IdStart).Text, 0),
            Step = Long(Get<TextBox>(IdStep).Text, 1),
        };
    }

    private const int MinNameColumnWidth = 120;
    private const int SizeColumnWidth = 72;
    private int _lastListWidth;

    public override void Activate()
    {
        var list = Main.List;
        _lastListWidth = list.ClientSize.Width;
        var w = CalculateColumnWidths(list);
        list.SetColumns((Res.T("Name"), w[0], HorizontalAlignment.Left), (Res.T("Size"), w[1], HorizontalAlignment.Right));
        base.Activate();
    }

    private static int[] CalculateColumnWidths(FileListView list)
    {
        int nameWidth = Math.Max(list.ClientSize.Width - SizeColumnWidth, MinNameColumnWidth);
        return [nameWidth, SizeColumnWidth];
    }

    private void OnListClientSizeChanged(object? sender, EventArgs e)
    {
        if (!Visible)
        {
            return;
        }
        int width = Main.List.ClientSize.Width;
        if (width > 0 && width != _lastListWidth)
        {
            _lastListWidth = width;
            Main.List.SetColumnWidths(CalculateColumnWidths(Main.List));
        }
    }

    public override void RefreshList() => UpdatePreview();

    /// <summary>連番オブジェクトの一覧は並べ替えもグループ表示もできない（オリジナル版と同じ）。</summary>
    public override bool CanArrange => false;

    private void UpdatePreview()
    {
        if (_updating)
        {
            return;
        }
        var list = Main.List;
        list.EnsureGenericIcons();
        var options = Options();
        bool isFolder = options.Kind is NumberedObjectKind.EmptyFolder or NumberedObjectKind.CopiedFolder;
        list.BeginUpdate();
        list.Items.Clear();
        string size = isFolder ? "" : FileListView.FormatSize(NumberedObjectGenerator.ParseSize(options.Size));
        foreach (var name in NumberedObjectGenerator.Names(options))
        {
            var item = new ListViewItem(name, isFolder ? "<dir>" : "<new>");
            item.SubItems.Add(size);
            list.Items.Add(item);
        }
        list.EndUpdate();
        Main.UpdateCounts();
    }

    private void Create()
    {
        var options = Options();
        if (options.Amount <= 0)
        {
            MessageBox.Show(this, Res.T("Input Amount"), "Flexible Renamer");
            return;
        }
        using var progress = new ProgressDialog(Res, Res.T("Creating Objects..."));
        int failed = 0;
        progress.Run(this, () => failed = NumberedObjectGenerator.Create(Main.CurrentFolder, options, progress.Report));
        if (failed > 0)
        {
            MessageBox.Show(this, Res.T("Failured to File Copy!\n%s").Replace("%s", failed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal), "Flexible Renamer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Main.RefreshList();
        // オリジナル版のダイアログバーはフォーカスを持たないので、実行後は一覧に戻す（Enter で再実行されない）
        Main.List.Focus();
    }
}
