namespace ReflexRenamer.Core.Settings;

/// <summary>
/// 最近送ったフォルダ（MENU 170）。設定の <c>[RenameDestination]</c> に <c>Destination=</c> を古い順に並べて持ち、
/// メニューには新しい順に出す（オリジナル版の実測：debug-destination-tree）。
/// </summary>
public static class DestinationHistory
{
    public const string Section = "RenameDestination";

    /// <summary>メニューの枠（コマンド 32943〜32947）の数。</summary>
    public const int Limit = 5;

    /// <summary>新しい順。</summary>
    public static IReadOnlyList<string> Get(AppSettings settings) =>
        settings.GetList(Section, "Destination").Reverse().ToList();

    /// <summary>送り先を一番新しいものとして入れる（同じフォルダは前の位置から外す）。</summary>
    public static void Push(AppSettings settings, string folder)
    {
        var list = settings.GetList(Section, "Destination")
            .Where(v => !string.Equals(v, folder, StringComparison.OrdinalIgnoreCase))
            .ToList();
        list.Add(folder);
        bool structure = CopyFolderStructure(settings);
        settings.SetList(Section, "Destination", list.Skip(Math.Max(0, list.Count - Limit)));
        // オリジナル版は送り先と一緒に CopyFolderStructure も書く
        settings.Set(Section, "CopyFolderStructure", structure);
    }

    /// <summary>送り先にサブフォルダを作成（CopyFolderStructure、既定はオン）。オリジナル版ではメニュー項目が常に無効で、値は保存されるだけ。</summary>
    public static bool CopyFolderStructure(AppSettings settings) => settings.GetBool(Section, "CopyFolderStructure", true);
}
