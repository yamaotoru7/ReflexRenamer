namespace FlexibleRenamer.Core.Rename;

/// <summary>
/// 一覧の既定の並び順。オリジナル版は並べ替えをせず、NTFS の列挙順（名前を大文字にしたコード順）のまま表示する。
/// </summary>
public sealed class FileOrder : IComparer<string>
{
    public static readonly FileOrder Ntfs = new();

    public int Compare(string? x, string? y) =>
        string.CompareOrdinal(x?.ToUpperInvariant(), y?.ToUpperInvariant());
}
