namespace ReflexRenamer.Ui;

/// <summary>
/// 入力欄のツールチップ。オリジナル版は文字列テーブル（docs/spec/resources.md の「文字列テーブル」）の文を使う。
/// コントロールとの対応はコードに埋め込まれていて抽出できないので、意味から対応付けた。
/// </summary>
public static class Tooltips
{
    private static readonly Dictionary<int, Dictionary<int, string>> Table = new()
    {
        [143] = new()
        {
            [1033] = "Rename file/folder.",
            [1057] = "Can define plural filter(wildcard), Separating by semicolon(;)",
            [1024] = "Recursive Under Subfolders",
            [1052] = "Depth of the Subfolder to Search",
            [1025] = "Flexible Rename using Wildcard or Regular-Expression",
            [1048] = "Recall Already Defined SearchFor/ReplaceWith",
            [1037] = "Search by Wildcard",
            [1038] = "Search by Regular-Expression",
            [1039] = "Replace All Chars in Search List\nwith Equivalent Char in Replace List",
            [1051] = "Options Concerning Regular-Expression",
            [1045] = "Support to Search",
            [1046] = "Support to Replace",
            [1026] = "Meta-expressions are intactly treated as syntax of script. To deal as a string, put in double-quotations(Ex.\"\\0\").",
            [1047] = "List of Supported Function",
            [1054] = "Sort by Condition and Renumber",
            [1049] = "Options Concerning Rename",
            [1011] = "Rename the files in the preview list",
            [1023] = "Undo last renaming operation",
            [1146] = "Operation to Perform",
        },
        [146] = new()
        {
            [1014] = "Available to Settings Under the Function List",
            [1032] = "Enter Advanced Rename mode with the selected menu item",
        },
        [170] = new()
        {
            [1024] = "Recursive Under Subfolders",
            [1052] = "Depth of the Subfolder to Search",
            [1048] = "Recall Already Defined SearchFor/ReplaceWith",
            [1019] = "Format of New Name",
            [1046] = "List of Supported Tag-Informatoin",
            [1030] = "Edit property definition",
            [1026] = "Meta-expressions are intactly treated as syntax of script. To deal as a string, put in double-quotations(Ex.\"\\0\").",
            [1047] = "List of Supported Function",
            [1054] = "Sort by Condition and Renumber",
            [1049] = "Options Concerning Rename",
            [1011] = "Rename the files in the preview list",
            [1023] = "Undo last renaming operation",
            [1146] = "Operation to Perform",
        },
        [141] = new()
        {
            [1024] = "Recursive Under Subfolders",
            [1025] = "Modify Folder Timestamp",
            [1125] = "Days to Shift",
            [1047] = "Time to Shift",
            [1045] = "Lower Limit of DateTime on Random-Generation",
            [1046] = "Upper Limit of DateTime on Random-Generation",
            [1142] = "Modify to photo taken date of EXIF in case of exist",
            [1011] = "Start to Modify Attributes",
        },
        [142] = new()
        {
            [1077] = "Create New Empty File",
            [1078] = "Copy the Existing File",
            [1079] = "Create New Empty Folder",
            [1080] = "Copy the Existing Folder",
            [1073] = "Select Source File for Copy",
            [1074] = "Select Source Folder for Copy",
            [1018] = "Enter Name Here",
            [1011] = "Start to Create Objects",
        },
    };

    /// <summary>ダイアログのコントロールにツールチップを付ける。</summary>
    public static void Apply(int dialogId, ControlMap map, AppResources res, ToolTip toolTip)
    {
        if (!Table.TryGetValue(dialogId, out var entries))
        {
            return;
        }
        foreach (var (id, key) in entries)
        {
            if (map.TryGet(id, out var control))
            {
                toolTip.SetToolTip(control, res.T(key));
            }
        }
    }
}
