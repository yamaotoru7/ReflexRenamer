using System.Globalization;
using System.Text;

namespace FlexibleRenamer.Core.Rename;

/// <summary>
/// 置換処理の3段目：連続する <c>?</c> を連番（桁数は <c>?</c> の個数でゼロ埋め）に、
/// <c>*</c> をランダムな数字1桁にする。
/// </summary>
public static class NumberExpander
{
    /// <param name="randomDigits">
    /// <c>*</c> をランダムな数字にするか。スクリプトを使うときは <c>*</c> を掛け算などに使うので、そのまま残す
    /// （オリジナル版の実測：golden script-order の so-number-start、so-star-probe）。
    /// </param>
    public static string Expand(string text, long number, RenameOptions options, bool randomDigits = true)
    {
        if (text.IndexOfAny(randomDigits ? ['?', '*'] : ['?']) < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '?')
            {
                int run = 1;
                while (i + 1 < text.Length && text[i + 1] == '?')
                {
                    run++;
                    i++;
                }
                sb.Append(Format(number, run, options.NumberType));
            }
            else if (c == '*' && randomDigits)
            {
                sb.Append((char)('0' + options.Random.Next(10)));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public static string Format(long number, int width, NumberType type) => type switch
    {
        NumberType.Hexadecimal => number.ToString("x", CultureInfo.InvariantCulture).PadLeft(width, '0'),
        NumberType.Alphabetical => Alphabetical(number).PadLeft(width, 'a'),
        _ => number < 0
            ? "-" + (-number).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')
            : number.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0'),
    };

    /// <summary>0 → a、25 → z、26 → ba のような26進数（a がゼロ）。</summary>
    private static string Alphabetical(long number)
    {
        if (number <= 0)
        {
            return "a";
        }
        var sb = new StringBuilder();
        while (number > 0)
        {
            sb.Insert(0, (char)('a' + number % 26));
            number /= 26;
        }
        return sb.ToString();
    }
}
