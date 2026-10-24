using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReflexRenamer.Core.Scripting;

/// <summary>
/// StrFunc.dll の <c>sprintf</c>／<c>vsprintf</c> と同じ書式化を行う（VC++ 2008 世代の CRT の swprintf 相当）。
/// </summary>
/// <remarks>
/// 原本は引数の VARIANT を 32bit の va_list（4バイト単位）に積んでから CRT に渡す。そのため型と書式が合わないと、
/// 隣の引数とつながった値として読まれる。実測（docs/research/strfunc-sample.json）から分かった積み方：
/// <list type="bullet">
/// <item>Integer・Long・Boolean は4バイト、Single は float のビット列のまま4バイト、Double は8バイト。</item>
/// <item>文字列はポインター（4バイト）。<c>%d</c> などで読むとアドレスになる（移植版では 0 とする）。</item>
/// <item>Byte・Currency・Date・Null・Empty などは何も積まない。配列は要素を順に（入れ子も）展開する。</item>
/// <item>足りない引数は 0 として読む。結果は最初の NUL 文字で切れる。</item>
/// </list>
/// </remarks>
public static class CFormat
{
    private readonly record struct Slot(uint Value, string? Text);

    /// <summary>引数を原本と同じ規則で積んで書式化する。配列は展開する。</summary>
    public static string Format(string format, IEnumerable<object?> args)
    {
        var slots = new List<Slot>();
        foreach (var arg in args) Push(slots, arg);
        string result = new Formatter(format, slots).Run();
        int nul = result.IndexOf('\0');
        return nul < 0 ? result : result[..nul];
    }

    private static void Push(List<Slot> slots, object? value)
    {
        switch (value)
        {
            case short s: slots.Add(new((uint)(int)s, null)); break;
            case int i: slots.Add(new((uint)i, null)); break;
            case bool b: slots.Add(new(b ? 0xFFFFFFFFu : 0u, null)); break;
            case float f: slots.Add(new(BitConverter.SingleToUInt32Bits(f), null)); break;
            case double d:
                ulong bits = BitConverter.DoubleToUInt64Bits(d);
                slots.Add(new((uint)bits, null));
                slots.Add(new((uint)(bits >> 32), null));
                break;
            case string text: slots.Add(new(0, text)); break;
            case Array array:
                foreach (var item in array) Push(slots, item);
                break;
            default: break; // Byte・Currency・Date・Null・Empty・オブジェクトは積まない
        }
    }

    private sealed class Formatter(string format, List<Slot> slots)
    {
        private int _next;
        private readonly StringBuilder _out = new();

        private Slot Take() => _next < slots.Count ? slots[_next++] : new Slot(0, null);
        private uint TakeInt() => Take().Value;
        private ulong TakeLong() { ulong low = TakeInt(); return low | ((ulong)TakeInt() << 32); }
        private double TakeDouble() => BitConverter.UInt64BitsToDouble(TakeLong());

        public string Run()
        {
            int i = 0;
            while (i < format.Length)
            {
                char c = format[i++];
                if (c != '%') { _out.Append(c); continue; }
                bool left = false, plus = false, space = false, alt = false, zero = false;
                for (; i < format.Length; i++)
                {
                    switch (format[i])
                    {
                        case '-': left = true; continue;
                        case '+': plus = true; continue;
                        case ' ': space = true; continue;
                        case '#': alt = true; continue;
                        case '0': zero = true; continue;
                    }
                    break;
                }
                int width = 0;
                if (i < format.Length && format[i] == '*')
                {
                    i++;
                    width = (int)TakeInt();
                    if (width < 0) { left = true; width = -width; }
                }
                else
                {
                    while (i < format.Length && char.IsAsciiDigit(format[i])) width = width * 10 + (format[i++] - '0');
                }
                int precision = -1;
                if (i < format.Length && format[i] == '.')
                {
                    i++;
                    precision = 0;
                    if (i < format.Length && format[i] == '*')
                    {
                        i++;
                        precision = (int)TakeInt();
                        if (precision < 0) precision = -1;
                    }
                    else
                    {
                        while (i < format.Length && char.IsAsciiDigit(format[i])) precision = precision * 10 + (format[i++] - '0');
                    }
                }
                bool isShort = false, isLong = false, isWide = false, is64 = false;
                for (; i < format.Length; i++)
                {
                    char m = format[i];
                    if (m == 'h') { isShort = true; continue; }
                    if (m == 'l')
                    {
                        if (i + 1 < format.Length && format[i + 1] == 'l') { is64 = true; i++; }
                        else isLong = true;
                        continue;
                    }
                    if (m == 'L') continue;
                    if (m == 'w') { isWide = true; continue; }
                    if (m == 'I')
                    {
                        if (i + 2 < format.Length && format[i + 1] == '6' && format[i + 2] == '4') { is64 = true; i += 2; }
                        else if (i + 2 < format.Length && format[i + 1] == '3' && format[i + 2] == '2') { i += 2; }
                        continue;
                    }
                    break;
                }
                if (i >= format.Length) break; // 書式の途中で終わったら何も出さない
                char conv = format[i++];
                var spec = new Spec(left, plus, space, alt, zero, width, precision);
                switch (conv)
                {
                    case 'c':
                    case 'C':
                        {
                            uint v = TakeInt();
                            bool narrow = conv == 'C' ? !(isLong || isWide) : isShort;
                            string text = narrow ? Ansi([(byte)v]) : ((char)(ushort)v).ToString();
                            Pad(text, spec);
                            break;
                        }
                    case 's':
                    case 'S':
                        {
                            var slot = Take();
                            bool narrow = conv == 'S' ? !(isLong || isWide) : isShort;
                            string text = slot.Text is null ? "(null)" : narrow ? NarrowView(slot.Text) : CutAtNul(slot.Text);
                            if (precision >= 0 && text.Length > precision) text = text[..precision];
                            Pad(text, spec);
                            break;
                        }
                    case 'd':
                    case 'i':
                        {
                            long v = is64 ? (long)TakeLong() : isShort ? (short)TakeInt() : (int)TakeInt();
                            Integer(v < 0 ? (ulong)(-(v + 1)) + 1 : (ulong)v, v < 0, 10, false, spec, signed: true);
                            break;
                        }
                    case 'u':
                    case 'o':
                    case 'x':
                    case 'X':
                        {
                            ulong v = is64 ? TakeLong() : isShort ? (ushort)TakeInt() : TakeInt();
                            int radix = conv == 'o' ? 8 : conv == 'u' ? 10 : 16;
                            Integer(v, false, radix, conv == 'X', spec, signed: false);
                            break;
                        }
                    case 'p':
                        Integer(TakeInt(), false, 16, true, spec with { Precision = 8, Alt = false }, signed: false);
                        break;
                    case 'e':
                    case 'E':
                    case 'f':
                    case 'g':
                    case 'G':
                    case 'a':
                    case 'A':
                        Floating(TakeDouble(), conv, spec);
                        break;
                    case 'n':
                        TakeInt(); // 原本はプロセスが落ちる
                        break;
                    case 'Z':
                        Take(); // 原本はプロセスが落ちる
                        break;
                    default:
                        _out.Append(conv); // %% や未知の文字はそのまま出す（幅は使わない）
                        break;
                }
            }
            return _out.ToString();
        }

        private readonly record struct Spec(bool Left, bool Plus, bool Space, bool Alt, bool Zero, int Width, int Precision);

        private void Pad(string text, Spec spec) => Pad("", text, spec);

        /// <summary>符号や 0x などの接頭辞と本体を幅に合わせて出す。0 埋めは接頭辞の後ろに入る。</summary>
        private void Pad(string prefix, string body, Spec spec)
        {
            int fill = spec.Width - prefix.Length - body.Length;
            if (fill <= 0) { _out.Append(prefix).Append(body); return; }
            if (spec.Left) { _out.Append(prefix).Append(body).Append(' ', fill); return; }
            if (spec.Zero) { _out.Append(prefix).Append('0', fill).Append(body); return; }
            _out.Append(' ', fill).Append(prefix).Append(body);
        }

        private void Integer(ulong magnitude, bool negative, int radix, bool upper, Spec spec, bool signed)
        {
            string digits = magnitude == 0 ? "0" : radix switch
            {
                10 => magnitude.ToString(CultureInfo.InvariantCulture),
                16 => magnitude.ToString(upper ? "X" : "x", CultureInfo.InvariantCulture),
                _ => Convert.ToString((long)magnitude, 8),
            };
            if (spec.Precision >= 0)
            {
                spec = spec with { Zero = false };
                if (spec.Precision == 0 && magnitude == 0) digits = "";
                else if (digits.Length < spec.Precision) digits = new string('0', spec.Precision - digits.Length) + digits;
            }
            string prefix = "";
            if (signed)
            {
                prefix = negative ? "-" : spec.Plus ? "+" : spec.Space ? " " : "";
            }
            else if (spec.Alt)
            {
                if (radix == 16 && magnitude != 0) prefix = upper ? "0X" : "0x";
                else if (radix == 8 && !digits.StartsWith('0')) digits = "0" + digits;
            }
            Pad(prefix, digits, spec);
        }

        private void Floating(double value, char conv, Spec spec)
        {
            ulong bits = BitConverter.DoubleToUInt64Bits(value);
            bool negative = (bits >> 63) != 0;
            string sign = negative ? "-" : spec.Plus ? "+" : spec.Space ? " " : "";
            char lower = char.ToLowerInvariant(conv);
            bool upper = char.IsUpper(conv);
            string body;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                body = Special(bits, lower, spec);
            }
            else if (lower == 'a')
            {
                body = Hex(bits, spec.Precision < 0 ? 6 : spec.Precision, spec.Alt);
            }
            else
            {
                var digits = Digits.Of(Math.Abs(value));
                body = lower switch
                {
                    'f' => Fixed(digits, spec.Precision < 0 ? 6 : spec.Precision, spec.Alt),
                    'e' => Exponent(digits, spec.Precision < 0 ? 6 : spec.Precision, spec.Alt),
                    _ => General(digits, spec.Precision < 0 ? 6 : spec.Precision == 0 ? 1 : spec.Precision, spec.Alt),
                };
            }
            if (upper) body = body.ToUpperInvariant();
            Pad(sign, body, spec);
        }

        /// <summary>無限大・非数は「1.#INF」などを数字の列と見なして書式化する（%.2f で 1.#J になる）。</summary>
        private static string Special(ulong bits, char conv, Spec spec)
        {
            ulong mantissa = bits & 0xFFFFFFFFFFFFFUL;
            string frac = mantissa == 0 ? "#INF"
                : (bits >> 63) != 0 && mantissa == 0x8000000000000UL ? "#IND"
                : (mantissa & 0x8000000000000UL) != 0 ? "#QNAN" : "#SNAN";
            int precision = spec.Precision < 0 ? 6 : spec.Precision;
            if (conv == 'g')
            {
                precision = (precision == 0 ? 1 : precision) - 1;
            }
            string text = RoundChars(frac, precision);
            string result = "1" + (text.Length > 0 || spec.Alt ? "." + text : "");
            if (conv == 'g' && !spec.Alt) result = CropZeros(result);
            if (conv == 'e') result += "e+000";
            return result;
        }

        private static string RoundChars(string frac, int precision)
        {
            if (precision >= frac.Length) return frac + new string('0', precision - frac.Length);
            var chars = frac[..precision].ToCharArray();
            if (precision > 0 && frac[precision] >= '5') chars[precision - 1]++;
            return new string(chars);
        }

        private static string Fixed(Digits digits, int precision, bool alt)
        {
            var d = digits.Round(digits.Exponent + 1 + precision);
            var integer = new StringBuilder();
            if (d.Exponent < 0) integer.Append('0');
            else for (int k = 0; k <= d.Exponent; k++) integer.Append(d.At(k));
            var fraction = new StringBuilder();
            for (int k = 1; k <= precision; k++) fraction.Append(d.At(d.Exponent + k));
            return precision > 0 || alt ? integer + "." + fraction : integer.ToString();
        }

        private static string Exponent(Digits digits, int precision, bool alt)
        {
            var d = digits.Round(precision + 1);
            var text = new StringBuilder();
            text.Append(d.At(0));
            if (precision > 0 || alt) text.Append('.');
            for (int k = 1; k <= precision; k++) text.Append(d.At(k));
            int exponent = d.IsZero ? 0 : d.Exponent;
            text.Append('e').Append(exponent < 0 ? '-' : '+').Append(Math.Abs(exponent).ToString("000", CultureInfo.InvariantCulture));
            return text.ToString();
        }

        private static string General(Digits digits, int precision, bool alt)
        {
            var rounded = digits.Round(precision);
            int magnitude = rounded.IsZero ? 0 : rounded.Exponent;
            string text = magnitude < -4 || magnitude >= precision
                ? Exponent(digits, precision - 1, alt)
                : Fixed(digits, precision - 1 - magnitude, alt);
            return alt ? text : CropZeros(text);
        }

        /// <summary>小数部の末尾の 0（と、残らなければ小数点）を取り除く。指数部はそのまま。</summary>
        private static string CropZeros(string text)
        {
            int e = text.IndexOf('e');
            string mantissa = e < 0 ? text : text[..e];
            string rest = e < 0 ? "" : text[e..];
            if (mantissa.Contains('.')) mantissa = mantissa.TrimEnd('0').TrimEnd('.');
            return mantissa + rest;
        }

        /// <summary>%a：仮数を16進で precision 桁（既定6桁）、指数は10進で桁をそろえない。</summary>
        private static string Hex(ulong bits, int precision, bool alt)
        {
            int rawExponent = (int)((bits >> 52) & 0x7FF);
            ulong mantissa = bits & 0xFFFFFFFFFFFFFUL;
            int lead = rawExponent == 0 ? 0 : 1;
            int exponent = rawExponent == 0 ? (mantissa == 0 ? 0 : -1022) : rawExponent - 1023;
            // 52bit（13桁）の仮数を precision 桁に丸める（次の桁が 8 以上なら切り上げ）
            var value = new BigInteger(mantissa);
            if (precision < 13)
            {
                int drop = (13 - precision) * 4;
                var rest = value & ((BigInteger.One << drop) - 1);
                value >>= drop;
                if (rest >= BigInteger.One << (drop - 1)) value++;
                if (value >> (precision * 4) != 0) { lead++; value &= (BigInteger.One << (precision * 4)) - 1; }
            }
            else
            {
                value <<= (precision - 13) * 4;
            }
            string fraction = precision == 0 ? "" : value.ToString("x" + precision, CultureInfo.InvariantCulture);
            if (fraction.Length > precision) fraction = fraction[^precision..];
            string point = precision > 0 || alt ? "." : "";
            return $"0x{lead}{point}{fraction}p{(exponent < 0 ? "-" : "+")}{Math.Abs(exponent)}";
        }

        private static string CutAtNul(string text)
        {
            int nul = text.IndexOf('\0');
            return nul < 0 ? text : text[..nul];
        }

        /// <summary>%S：UTF-16 の文字列を ANSI の文字列として読んだときの見え方（最初の 0 のバイトで切れる）。</summary>
        private static string NarrowView(string text)
        {
            var bytes = new List<byte>();
            foreach (byte b in Encoding.Unicode.GetBytes(text))
            {
                if (b == 0) break;
                bytes.Add(b);
            }
            return Ansi([.. bytes]);
        }
    }

    /// <summary>17桁の有効数字（旧 CRT の _fltout と同じ）と、先頭の桁の位。</summary>
    private readonly struct Digits
    {
        private readonly string _digits;

        private Digits(string digits, int exponent)
        {
            _digits = digits;
            Exponent = exponent;
        }

        public int Exponent { get; }
        public bool IsZero => _digits.All(c => c == '0');

        public static Digits Of(double value)
        {
            if (value == 0) return new Digits("0", 0);
            string text = value.ToString("E16", CultureInfo.InvariantCulture); // d.dddddddddddddddde+xxx
            int e = text.IndexOf('E');
            return new Digits(text[..1] + text[2..e], int.Parse(text[(e + 1)..], CultureInfo.InvariantCulture));
        }

        /// <summary>先頭から数えて index 番目の桁。範囲外は 0。</summary>
        public char At(int index) => index >= 0 && index < _digits.Length ? _digits[index] : '0';

        /// <summary>先頭から count 桁を残すように四捨五入する（次の桁が 5 以上なら切り上げ）。</summary>
        public Digits Round(int count)
        {
            if (count >= _digits.Length) return this;
            if (count < 0) return new Digits("0", Exponent);
            if (_digits[count] < '5') return new Digits(_digits[..count] + new string('0', _digits.Length - count), Exponent);
            var chars = _digits[..count].ToCharArray();
            int k = count - 1;
            for (; k >= 0; k--)
            {
                if (chars[k] == '9') { chars[k] = '0'; continue; }
                chars[k]++;
                break;
            }
            string kept = new(chars);
            return k < 0 ? new Digits("1" + kept, Exponent + 1) : new Digits(kept, Exponent);
        }
    }

    private static string Ansi(byte[] bytes)
    {
        if (bytes.Length == 0) return "";
        int length = MultiByteToWideChar(0, 0, bytes, bytes.Length, null, 0);
        if (length <= 0) return "";
        var buffer = new char[length];
        MultiByteToWideChar(0, 0, bytes, bytes.Length, buffer, length);
        return new string(buffer);
    }

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int MultiByteToWideChar(uint codePage, uint flags, byte[] source, int sourceLength, [Out] char[]? destination, int destinationLength);
}
