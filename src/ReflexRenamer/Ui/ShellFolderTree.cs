using System.Runtime.InteropServices;

namespace ReflexRenamer.Ui;

/// <summary>
/// 左側のフォルダツリー。オリジナル版と同じく、シェルの名前空間（デスクトップを根に、ホーム、PC、ドライブ…）を
/// IShellFolder で列挙する。隠しフォルダも含める（オリジナル版は $RECYCLE.BIN も表示する）。
/// </summary>
public sealed class ShellFolderTree : TreeView
{
    private readonly ImageList _images = new() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
    private bool _selecting;

    public ShellFolderTree()
    {
        HideSelection = false;
        ShowLines = false;
        FullRowSelect = true;
        HotTracking = true;
        ItemHeight = Font.Height + 6;
        ImageList = _images;
        BeforeExpand += (_, e) => LoadChildren(e.Node!);
        AfterSelect += (_, e) =>
        {
            // ユーザーの操作で選んだときだけ（コードからの選択や、ツリーが最初の項目を自動で選んだときは除く）
            if (!_selecting && e.Action != TreeViewAction.Unknown && e.Node?.Tag is ShellNode { Path: { } path } && Directory.Exists(path))
            {
                FolderSelected?.Invoke(path);
            }
        };
        RefreshTree();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SetWindowTheme(Handle, "Explorer", null);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ItemHeight = Font.Height + 6;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    internal static extern int SetWindowTheme(nint hwnd, string? appName, string? idList);

    public event Action<string>? FolderSelected;

    public void RefreshTree()
    {
        string? selected = (SelectedNode?.Tag as ShellNode)?.Path;
        BeginUpdate();
        foreach (TreeNode n in Nodes)
        {
            DisposeNode(n);
        }
        Nodes.Clear();
        Shell.SHGetDesktopFolder(out var desktop);
        var root = ShellNode.Desktop(desktop);
        var rootNode = CreateNode(root);
        Nodes.Add(rootNode);
        LoadChildren(rootNode);
        rootNode.Expand();
        EndUpdate();
        if (selected is not null)
        {
            SelectPath(selected);
        }
    }

    /// <summary>パスに対応するノードまで展開して選ぶ（FolderSelected は起こさない）。</summary>
    public void SelectPath(string path)
    {
        string full = Path.GetFullPath(path).TrimEnd('\\');
        _selecting = true;
        try
        {
            var node = Find(Nodes, full);
            if (node is not null)
            {
                SelectedNode = node;
                node.EnsureVisible();
            }
        }
        finally
        {
            _selecting = false;
        }
    }

    private TreeNode? Find(TreeNodeCollection nodes, string full)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is not ShellNode shell)
            {
                continue;
            }
            string? p = shell.Path?.TrimEnd('\\');
            if (p is not null && p.Equals(full, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
            // パスを持つフォルダはその配下だけを、仮想フォルダ（デスクトップ、PC）は中身をすべて探す
            bool descend = p is null || full.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase);
            if (!descend)
            {
                continue;
            }
            LoadChildren(node);
            var found = Find(node.Nodes, full);
            if (found is not null)
            {
                node.Expand();
                return found;
            }
        }
        return null;
    }

    private TreeNode CreateNode(ShellNode shell)
    {
        var node = new TreeNode(shell.Name) { Tag = shell, ImageKey = shell.IconKey(_images), SelectedImageKey = shell.IconKey(_images) };
        if (shell.HasSubfolders)
        {
            node.Nodes.Add(new TreeNode("\0"));
        }
        return node;
    }

    private void LoadChildren(TreeNode node)
    {
        if (node.Tag is not ShellNode shell || node.Nodes.Count != 1 || node.Nodes[0].Text != "\0")
        {
            return;
        }
        node.Nodes.Clear();
        foreach (var child in shell.Children())
        {
            node.Nodes.Add(CreateNode(child));
        }
    }

    private static void DisposeNode(TreeNode node)
    {
        (node.Tag as ShellNode)?.Dispose();
        foreach (TreeNode c in node.Nodes)
        {
            DisposeNode(c);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (TreeNode n in Nodes)
            {
                DisposeNode(n);
            }
            _images.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>シェル名前空間の1フォルダ。絶対 PIDL と、その IShellFolder を持つ。</summary>
internal sealed class ShellNode : IDisposable
{
    private readonly Shell.IShellFolder? _parent;
    private readonly nint _absolutePidl;
    private readonly nint _childPidl;

    private ShellNode(Shell.IShellFolder? parent, nint absolutePidl, nint childPidl, string name, string? path, bool hasSubfolders)
    {
        _parent = parent;
        _absolutePidl = absolutePidl;
        _childPidl = childPidl;
        Name = name;
        Path = path;
        HasSubfolders = hasSubfolders;
    }

    public string Name { get; }

    /// <summary>ファイルシステム上のパス。仮想フォルダなら null。</summary>
    public string? Path { get; }

    public bool HasSubfolders { get; }

    public static ShellNode Desktop(Shell.IShellFolder desktop)
    {
        Shell.SHGetKnownFolderIDList(Shell.FolderIdDesktop, 0, 0, out nint pidl);
        return new ShellNode(null, pidl, 0, Shell.DisplayName(pidl, Shell.SigdnNormalDisplay), Shell.DisplayName(pidl, Shell.SigdnFileSysPath), true) { Folder = desktop };
    }

    private Shell.IShellFolder? Folder { get; set; }

    private Shell.IShellFolder? Bind()
    {
        if (Folder is null && _parent is not null)
        {
            var iid = Shell.IidShellFolder;
            if (_parent.BindToObject(_childPidl, 0, ref iid, out object o) == 0)
            {
                Folder = (Shell.IShellFolder)o;
            }
        }
        return Folder;
    }

    public IEnumerable<ShellNode> Children()
    {
        var folder = Bind();
        // エクスプローラーのナビゲーション ウィンドウと同じ列挙（ホーム、ギャラリー、OneDrive、PC…）。隠しフォルダも含める
        if (folder is null || folder.EnumObjects(0, Shell.ShcontfFolders | Shell.ShcontfIncludeHidden | Shell.ShcontfNavigationEnum, out var enumerator) != 0 || enumerator is null)
        {
            return [];
        }
        var children = new List<nint>();
        var pidls = new nint[1];
        while (enumerator.Next(1, pidls, out uint fetched) == 0 && fetched == 1)
        {
            children.Add(pidls[0]);
        }
        // シェルの並び順（名前順。ドライブやライブラリは種類ごと）にする
        children.Sort((a, b) => (short)(folder.CompareIDs(0, a, b) & 0xFFFF));

        var result = new List<ShellNode>(children.Count);
        foreach (nint child in children)
        {
            nint absolute = Shell.ILCombine(_absolutePidl, child);
            uint attributes = Shell.SfgaoHasSubfolder | Shell.SfgaoFolder;
            folder.GetAttributesOf(1, [child], ref attributes);
            result.Add(new ShellNode(
                folder,
                absolute,
                child,
                Shell.DisplayName(absolute, Shell.SigdnNormalDisplay),
                Shell.DisplayName(absolute, Shell.SigdnFileSysPath),
                (attributes & Shell.SfgaoHasSubfolder) != 0));
        }
        return result;
    }

    public string IconKey(ImageList images)
    {
        // 通常のフォルダはアイコンを共有し、ドライブや仮想フォルダは個別にする
        string key = Path is not null && Path.Length > 3 ? "<folder>" : Name;
        if (!images.Images.ContainsKey(key))
        {
            var info = new Shell.ShFileInfo();
            if (Shell.SHGetFileInfo(_absolutePidl, 0, ref info, (uint)Marshal.SizeOf<Shell.ShFileInfo>(), Shell.ShgfiPidl | Shell.ShgfiIcon | Shell.ShgfiSmallIcon) != 0 && info.Icon != 0)
            {
                using var icon = Icon.FromHandle(info.Icon);
                images.Images.Add(key, (Icon)icon.Clone());
                Shell.DestroyIcon(info.Icon);
            }
        }
        return key;
    }

    public void Dispose()
    {
        if (_childPidl != 0)
        {
            Shell.ILFree(_childPidl);
        }
        if (_absolutePidl != 0)
        {
            Shell.ILFree(_absolutePidl);
        }
    }
}

/// <summary>シェル名前空間の API。</summary>
internal static class Shell
{
    public const uint ShcontfFolders = 0x20;
    public const uint ShcontfIncludeHidden = 0x80;
    public const uint ShcontfNavigationEnum = 0x1000;
    public const uint SfgaoFolder = 0x20000000;
    public const uint SfgaoHasSubfolder = 0x80000000;
    public const uint SigdnNormalDisplay = 0;
    public const uint SigdnFileSysPath = 0x80058000;
    public const uint ShgfiIcon = 0x100;
    public const uint ShgfiSmallIcon = 0x1;
    public const uint ShgfiPidl = 0x8;

    public static readonly Guid IidShellFolder = new("000214E6-0000-0000-C000-000000000046");
    public static readonly Guid FolderIdDesktop = new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");

    public static string DisplayName(nint pidl, uint sigdn)
    {
        if (SHGetNameFromIDList(pidl, sigdn, out nint name) != 0 || name == 0)
        {
            return sigdn == SigdnFileSysPath ? null! : "";
        }
        string s = Marshal.PtrToStringUni(name)!;
        Marshal.FreeCoTaskMem(name);
        return s;
    }

    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellFolder
    {
        [PreserveSig]
        int ParseDisplayName(nint hwnd, nint bindContext, [MarshalAs(UnmanagedType.LPWStr)] string displayName, out uint eaten, out nint pidl, ref uint attributes);

        [PreserveSig]
        int EnumObjects(nint hwnd, uint flags, out IEnumIDList? enumerator);

        [PreserveSig]
        int BindToObject(nint pidl, nint bindContext, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object obj);

        [PreserveSig]
        int BindToStorage(nint pidl, nint bindContext, ref Guid riid, out nint obj);

        [PreserveSig]
        int CompareIDs(nint lParam, nint pidl1, nint pidl2);

        [PreserveSig]
        int CreateViewObject(nint hwnd, ref Guid riid, out nint obj);

        [PreserveSig]
        int GetAttributesOf(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] nint[] pidls, ref uint attributes);

        [PreserveSig]
        int GetUIObjectOf(nint hwnd, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] nint[] pidls, ref Guid riid, nint reserved, out nint obj);

        [PreserveSig]
        int GetDisplayNameOf(nint pidl, uint flags, nint name);

        [PreserveSig]
        int SetNameOf(nint hwnd, nint pidl, [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out nint newPidl);
    }

    [ComImport]
    [Guid("000214F2-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumIDList
    {
        [PreserveSig]
        int Next(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] nint[] pidls, out uint fetched);

        [PreserveSig]
        int Skip(uint count);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone(out IEnumIDList enumerator);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ShFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32")]
    public static extern int SHGetDesktopFolder(out IShellFolder folder);

    [DllImport("shell32")]
    public static extern int SHGetKnownFolderIDList(in Guid folderId, uint flags, nint token, out nint pidl);

    [DllImport("shell32")]
    public static extern int SHGetNameFromIDList(nint pidl, uint sigdn, out nint name);

    [DllImport("shell32")]
    public static extern nint ILCombine(nint parent, nint child);

    [DllImport("shell32")]
    public static extern void ILFree(nint pidl);

    [DllImport("shell32", CharSet = CharSet.Unicode)]
    public static extern nint SHGetFileInfo(nint pidl, uint attributes, ref ShFileInfo info, uint size, uint flags);

    [DllImport("user32")]
    public static extern bool DestroyIcon(nint icon);
}
