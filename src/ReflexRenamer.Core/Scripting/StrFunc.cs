using System.Runtime.InteropServices;

namespace ReflexRenamer.Core.Scripting;

/// <summary>
/// オリジナル版付属の StrFunc.dll（ProgID <c>StrFunc.Wrapper</c>、32bit COM）と同じ関数。
/// 原本は Win32 の LCMapString／GetDateFormat／GetTimeFormat の薄いラッパーなので、同じ API を呼ぶ。
/// スクリプトからは <see cref="StrFuncObject"/> を通して使う。
/// </summary>
public static class StrFunc
{
    public const int LCMAP_LOWERCASE = 0x100;
    public const int LCMAP_UPPERCASE = 0x200;
    public const int LCMAP_PROPERCASE = 0x300;
    public const int LCMAP_SORTKEY = 0x400;
    public const int LCMAP_BYTEREV = 0x800;
    public const int LCMAP_HIRAGANA = 0x100000;
    public const int LCMAP_KATAKANA = 0x200000;
    public const int LCMAP_HALFWIDTH = 0x400000;
    public const int LCMAP_FULLWIDTH = 0x800000;
    public const int LCMAP_LINGUISTIC_CASING = 0x1000000;
    public const int LCMAP_SIMPLIFIED_CHINESE = 0x2000000;
    public const int LCMAP_TRADITIONAL_CHINESE = 0x4000000;

    public const int vbNone = 0;
    public const int vbUpperCase = 1;
    public const int vbLowerCase = 2;
    public const int vbProperCase = 3;
    public const int vbWide = 4;
    public const int vbNarrow = 8;
    public const int vbKatakana = 16;
    public const int vbHiragana = 32;
    public const int vbSimplifiedChinese = 256;
    public const int vbTraditionalChinese = 512;
    public const int vbLinguisticCasing = 1024;

    /// <summary>スクリプトから見える定数（名前と値）。順序は原本の型情報と同じ。</summary>
    public static IReadOnlyList<(string Name, int Value)> Constants { get; } =
    [
        ("LCMAP_LOWERCASE", LCMAP_LOWERCASE), ("LCMAP_UPPERCASE", LCMAP_UPPERCASE), ("LCMAP_PROPERCASE", LCMAP_PROPERCASE),
        ("LCMAP_SORTKEY", LCMAP_SORTKEY), ("LCMAP_BYTEREV", LCMAP_BYTEREV), ("LCMAP_HIRAGANA", LCMAP_HIRAGANA),
        ("LCMAP_KATAKANA", LCMAP_KATAKANA), ("LCMAP_HALFWIDTH", LCMAP_HALFWIDTH), ("LCMAP_FULLWIDTH", LCMAP_FULLWIDTH),
        ("LCMAP_LINGUISTIC_CASING", LCMAP_LINGUISTIC_CASING), ("LCMAP_SIMPLIFIED_CHINESE", LCMAP_SIMPLIFIED_CHINESE),
        ("LCMAP_TRADITIONAL_CHINESE", LCMAP_TRADITIONAL_CHINESE),
        ("NORM_IGNORENONSPACE", 0x2), ("NORM_IGNORESYMBOLS", 0x4), ("NORM_IGNORECASE", 0x1),
        ("NORM_IGNOREKANATYPE", 0x10000), ("NORM_IGNOREWIDTH", 0x20000), ("SORT_STRINGSORT", 0x1000),
        ("vbNone", vbNone), ("vbUpperCase", vbUpperCase), ("vbLowerCase", vbLowerCase), ("vbProperCase", vbProperCase),
        ("vbWide", vbWide), ("vbNarrow", vbNarrow), ("vbKatakana", vbKatakana), ("vbHiragana", vbHiragana),
        ("vbSimplifiedChinese", vbSimplifiedChinese), ("vbTraditionalChinese", vbTraditionalChinese),
        ("vbLinguisticCasing", vbLinguisticCasing),
        ("LOCALE_NOUSEROVERRIDE", unchecked((int)0x80000000)), ("LOCALE_USE_CP_ACP", 0x40000000),
        ("DATE_SHORTDATE", 0x1), ("DATE_LONGDATE", 0x2), ("DATE_USE_ALT_CALENDAR", 0x4), ("DATE_YEARMONTH", 0x8),
        ("DATE_LTRREADING", 0x10), ("DATE_RTLREADING", 0x20),
        ("TIME_NOMINUTESORSECONDS", 0x1), ("TIME_NOSECONDS", 0x2), ("TIME_NOTIMEMARKER", 0x4), ("TIME_FORCE24HOURFORMAT", 0x8),
    ];

    /// <summary>
    /// Win32 の LCMapString。入力は最初の NUL 文字までで、失敗（フラグの組み合わせが不正など）すると空文字。
    /// LCMAP_SORTKEY の結果はバイト列を UTF-16 の文字として詰めたもの（原本と同じく NUL で切れる）。
    /// </summary>
    /// <remarks>
    /// LCMAP_PROPERCASE だけは Win32 に任せない（Win32 は _ や - の後も大文字にする）。原本は、ほかのフラグと
    /// LCMAP_LOWERCASE で変換した結果について、先頭と空白（iswspace）の直後の文字だけを LCMAP_UPPERCASE（言語規則なし）で大文字にする。
    /// </remarks>
    public static string LCMapString(int localeId, int mapFlags, string source)
    {
        source = CutAtNul(source);
        uint flags = unchecked((uint)mapFlags);
        if ((flags & LCMAP_PROPERCASE) != LCMAP_PROPERCASE) return Map(localeId, flags, source);
        string lower = Map(localeId, (flags & ~(uint)LCMAP_PROPERCASE) | LCMAP_LOWERCASE, source);
        var chars = lower.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (i > 0 && !IsSpace(chars[i - 1])) continue;
            string upper = Map(localeId, LCMAP_UPPERCASE, chars[i].ToString());
            if (upper.Length == 1) chars[i] = upper[0];
        }
        return new string(chars);
    }

    /// <summary>旧 CRT の iswspace と同じ判定。U+00FF までは CRT の表（U+0085 は空白でない）、それより上は C1_SPACE。</summary>
    private static bool IsSpace(char c)
    {
        if (c < 0x100) return c is (>= (char)9 and <= (char)13) or (char)32 or (char)0xA0;
        const uint CT_CTYPE1 = 1;
        const ushort C1_SPACE = 0x8;
        return GetStringTypeW(CT_CTYPE1, c.ToString(), 1, out ushort type) && (type & C1_SPACE) != 0;
    }

    private static string Map(int localeId, uint flags, string source)
    {
        int size = LCMapStringW((uint)localeId, flags, source, -1, null, 0);
        if (size <= 0) return "";
        bool sortKey = (flags & LCMAP_SORTKEY) != 0;
        var buffer = new char[sortKey ? size / 2 + 1 : size];
        if (LCMapStringW((uint)localeId, flags, source, -1, buffer, size) <= 0) return "";
        return CutAtNul(new string(buffer));
    }

    /// <summary>
    /// VB の StrConv に似た変換。Conversion の各ビットを LCMap のフラグに置き換えて LCMapString を呼ぶ。
    /// 知らないビットは無視する。vbNone（0）は変換なしではなく空文字になる。
    /// </summary>
    public static string StrConv(string source, int conversion, int localeId = 0)
    {
        int flags = 0;
        if ((conversion & vbUpperCase) != 0) flags |= LCMAP_UPPERCASE;
        if ((conversion & vbLowerCase) != 0) flags |= LCMAP_LOWERCASE;
        if ((conversion & vbWide) != 0) flags |= LCMAP_FULLWIDTH;
        if ((conversion & vbNarrow) != 0) flags |= LCMAP_HALFWIDTH;
        if ((conversion & vbKatakana) != 0) flags |= LCMAP_KATAKANA;
        if ((conversion & vbHiragana) != 0) flags |= LCMAP_HIRAGANA;
        if ((conversion & vbSimplifiedChinese) != 0) flags |= LCMAP_SIMPLIFIED_CHINESE;
        if ((conversion & vbTraditionalChinese) != 0) flags |= LCMAP_TRADITIONAL_CHINESE;
        if ((conversion & vbLinguisticCasing) != 0) flags |= LCMAP_LINGUISTIC_CASING;
        return LCMapString(localeId, flags, source);
    }

    /// <summary>C の sprintf。引数の扱いは <see cref="CFormat"/> を参照。</summary>
    public static string Sprintf(string format, params object?[] args) => CFormat.Format(format, args);

    /// <summary>Win32 の GetDateFormat。<paramref name="oleDate"/> は VT_DATE の値。失敗すると空文字。</summary>
    public static string GetDateFormat(int localeId, int flags, double oleDate, string format) =>
        TimeFormat(oleDate, (in SystemTime time, char[]? buffer, int size) => GetDateFormatW((uint)localeId, unchecked((uint)flags), in time, format, buffer, size));

    public static string GetDateFormat(int localeId, int flags, DateTime date, string format) => GetDateFormat(localeId, flags, date.ToOADate(), format);

    /// <summary>Win32 の GetTimeFormat。失敗すると空文字。</summary>
    public static string GetTimeFormat(int localeId, int flags, double oleDate, string format) =>
        TimeFormat(oleDate, (in SystemTime time, char[]? buffer, int size) => GetTimeFormatW((uint)localeId, unchecked((uint)flags), in time, format, buffer, size));

    public static string GetTimeFormat(int localeId, int flags, DateTime date, string format) => GetTimeFormat(localeId, flags, date.ToOADate(), format);

    private delegate int FormatCall(in SystemTime time, char[]? buffer, int size);

    private static string TimeFormat(double oleDate, FormatCall call)
    {
        if (VariantTimeToSystemTime(oleDate, out var time) == 0) return "";
        int size = call(in time, null, 0);
        if (size <= 0) return "";
        var buffer = new char[size];
        size = call(in time, buffer, size);
        return size <= 0 ? "" : new string(buffer, 0, size - 1);
    }

    private static string CutAtNul(string text)
    {
        int nul = text.IndexOf('\0');
        return nul < 0 ? text : text[..nul];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetStringTypeW(uint infoType, string source, int length, out ushort charType);

    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int LCMapStringW(uint locale, uint flags, string source, int sourceLength, [Out] char[]? destination, int destinationLength);

    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetDateFormatW(uint locale, uint flags, in SystemTime date, string format, [Out] char[]? buffer, int size);

    [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetTimeFormatW(uint locale, uint flags, in SystemTime time, string format, [Out] char[]? buffer, int size);

    [DllImport("oleaut32", ExactSpelling = true)]
    private static extern int VariantTimeToSystemTime(double time, out SystemTime systemTime);
}
