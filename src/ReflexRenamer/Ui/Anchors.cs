namespace ReflexRenamer.Ui;

/// <summary>
/// ダイアログ内のコントロールのアンカー。オリジナル版はフォームをペインいっぱいに広げ、
/// コントロールごとに右寄せ・伸縮・下寄せをしている。
/// 値は <c>tools/anchors.py</c> で、オリジナル版の実測位置（UI ツリー）とテンプレートを突き合わせて求めたもの。
/// 表にないコントロールは左上固定。
/// </summary>
public static class Anchors
{
    private static readonly Dictionary<int, Dictionary<int, AnchorStyles>> Table = new()
    {
        // リネーム（DIALOG 143）
        [143] = new()
        {
            [1057] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            [1048] = AnchorStyles.Right | AnchorStyles.Top,
            [1045] = AnchorStyles.Right | AnchorStyles.Top,
            [1010] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            [1046] = AnchorStyles.Right | AnchorStyles.Top,
            [1019] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            [1055] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            [1026] = AnchorStyles.Right | AnchorStyles.Top,
            [1047] = AnchorStyles.Right | AnchorStyles.Top,
            [1029] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1011] = AnchorStyles.Right | AnchorStyles.Bottom,
            [1049] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1023] = AnchorStyles.Right | AnchorStyles.Bottom,
            [1146] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        },
        // 属性（DIALOG 141）と連番オブジェクト（DIALOG 142）。下端の区切り線と実行ボタンだけ下に寄る
        [141] = new()
        {
            [1029] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1011] = AnchorStyles.Right | AnchorStyles.Bottom,
        },
        [142] = new()
        {
            [1029] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1011] = AnchorStyles.Right | AnchorStyles.Bottom,
        },
        // タグ・リネーム（DIALOG 170）。書式から下はページの下端に寄る
        [170] = new()
        {
            [1048] = AnchorStyles.Right | AnchorStyles.Top,
            [1055] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            [1160] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            [1024] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1053] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1052] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1090] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1040] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1046] = AnchorStyles.Right | AnchorStyles.Bottom,
            [1019] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            [1029] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1011] = AnchorStyles.Right | AnchorStyles.Bottom,
            [1049] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1023] = AnchorStyles.Right | AnchorStyles.Bottom,
            [1081] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1065] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1140] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1054] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1084] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1083] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1067] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1142] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1173] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1030] = AnchorStyles.Right | AnchorStyles.Top,
            [1047] = AnchorStyles.Right | AnchorStyles.Bottom,
            [1026] = AnchorStyles.Right | AnchorStyles.Bottom,
            [1088] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            [1146] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        },
        // 定型メニュー（DIALOG 146）。パラメータ欄はパネルの下端に寄る
        [146] = new()
        {
            [1014] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
            [1032] = AnchorStyles.Right | AnchorStyles.Top,
            [1081] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1065] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1140] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1030] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1031] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1138] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1023] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1019] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1136] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1082] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1066] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1141] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1021] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1018] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            [1028] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1044] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            [1022] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1045] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            [1024] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1025] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            [1026] = AnchorStyles.Left | AnchorStyles.Bottom,
            [1027] = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        },
    };

    public static AnchorStyles For(int dialogId, int controlId) =>
        Table.TryGetValue(dialogId, out var dialog) && dialog.TryGetValue(controlId, out var a)
            ? a
            : AnchorStyles.Left | AnchorStyles.Top;
}
