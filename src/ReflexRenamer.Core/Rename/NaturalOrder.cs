using System.Globalization;

namespace ReflexRenamer.Core.Rename;

/// <summary>
/// 一覧の見出しをクリックしたときの並び順（オリジナル版の実測、explore-sort）。
/// 数字の並びは数値として比べ（<c>song2</c> が <c>song10</c> より前）、それ以外は大文字小文字を区別しない文字コード順
/// （<c>README</c> が <c>report</c> より前、カタカナ → 全角英字 → 半角カナの順）。エクスプローラーのロケール順ではない。
/// </summary>
public sealed class NaturalOrder : IComparer<string>
{
    public static readonly NaturalOrder Instance = new();

    public int Compare(string? x, string? y)
    {
        x ??= "";
        y ??= "";
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (IsDigit(x[i]) && IsDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && IsDigit(x[i])) i++;
                while (j < y.Length && IsDigit(y[j])) j++;
                var a = x.AsSpan(si, i - si).TrimStart('0');
                var b = y.AsSpan(sj, j - sj).TrimStart('0');
                if (a.Length != b.Length)
                {
                    return a.Length < b.Length ? -1 : 1;
                }
                int digits = a.SequenceCompareTo(b);
                if (digits != 0)
                {
                    return digits;
                }
                continue;
            }
            char cx = char.ToUpperInvariant(x[i]);
            char cy = char.ToUpperInvariant(y[j]);
            if (cx != cy)
            {
                return cx < cy ? -1 : 1;
            }
            i++;
            j++;
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}
