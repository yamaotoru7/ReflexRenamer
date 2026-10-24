using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlexibleRenamer.Core.Scripting;

/// <summary>
/// スクリプトに渡す <c>StrFunc.Wrapper</c> 互換のオブジェクト（IDispatch）。
/// 引数の数・型の誤りは原本と同じ HRESULT を返すので、VBScript では同じエラー番号（450、13、438、451）になる。
/// </summary>
/// <remarks>
/// 原本との違い：参照渡し（変数をそのまま渡したとき）の日付・数値も値として読む。原本はポインターを値として読み、
/// 結果が不定になるかプロセスが落ちる。日付の引数に日付以外を渡したときは日付に変換する（原本は前回の値などが残る）。
/// </remarks>
public sealed class StrFuncObject
{
    private enum Member
    {
        LCMapString = 1,
        StrConv,
        Sprintf,
        Vsprintf,
        GetDateFormat,
        GetTimeFormat,
        FirstConstant = 100,
    }

    private static readonly Dictionary<string, int> Ids = CreateIds();

    private static Dictionary<string, int> CreateIds()
    {
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["LCMapString"] = (int)Member.LCMapString,
            ["StrConv"] = (int)Member.StrConv,
            ["sprintf"] = (int)Member.Sprintf,
            ["vsprintf"] = (int)Member.Vsprintf,
            ["GetDateFormat"] = (int)Member.GetDateFormat,
            ["GetTimeFormat"] = (int)Member.GetTimeFormat,
        };
        for (int i = 0; i < StrFunc.Constants.Count; i++) ids[StrFunc.Constants[i].Name] = (int)Member.FirstConstant + i;
        return ids;
    }

    /// <summary>COM の IUnknown ポインター（参照カウントを1つ持つ）を作る。呼び出し側が Release する。</summary>
    public nint CreateComPointer() => Wrappers.Instance.GetOrCreateComInterfaceForObject(this, CreateComInterfaceFlags.None);

    private const int S_OK = 0;
    private const int DISP_E_MEMBERNOTFOUND = unchecked((int)0x80020003);
    private const int DISP_E_TYPEMISMATCH = unchecked((int)0x80020005);
    private const int DISP_E_UNKNOWNNAME = unchecked((int)0x80020006);
    private const int DISP_E_NONAMEDARGS = unchecked((int)0x80020007);
    private const int DISP_E_BADINDEX = unchecked((int)0x8002000B);
    private const int DISP_E_BADPARAMCOUNT = unchecked((int)0x8002000E);
    private const int DISP_E_NOTACOLLECTION = unchecked((int)0x80020011);
    private const int E_FAIL = unchecked((int)0x80004005);
    private const ushort DISPATCH_METHOD = 1;
    private const ushort DISPATCH_PROPERTYGET = 2;
    private const ushort VT_I4 = 3;
    private const ushort VT_R8 = 5;
    private const ushort VT_DATE = 7;
    private const ushort VT_BSTR = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct Variant
    {
        public ushort Type, Reserved1, Reserved2, Reserved3;
        public nint Data1, Data2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct DispParams
    {
        public Variant* Args;
        public int* NamedArgs;
        public uint ArgCount;
        public uint NamedArgCount;
    }

    /// <summary>引数の変換に失敗した位置（原本の順序で 0 から）。</summary>
    private sealed class ArgumentFailure(int index, int hresult) : Exception
    {
        public int Index { get; } = index;
        public int HResult2 { get; } = hresult;
    }

    private unsafe int Invoke(int id, ushort flags, DispParams* parameters, Variant* result, uint* argError)
    {
        if ((flags & (DISPATCH_METHOD | DISPATCH_PROPERTYGET)) == 0) return DISP_E_MEMBERNOTFOUND;
        int count = (int)parameters->ArgCount;
        if (parameters->NamedArgCount > 0) return DISP_E_NONAMEDARGS;
        Variant* args = parameters->Args;

        object value;
        try
        {
            if (id >= (int)Member.FirstConstant)
            {
                int index = id - (int)Member.FirstConstant;
                if (index >= StrFunc.Constants.Count) return DISP_E_MEMBERNOTFOUND;
                if (count > 0) return DISP_E_NOTACOLLECTION;
                value = StrFunc.Constants[index].Value;
            }
            else
            {
                switch ((Member)id)
                {
                    case Member.LCMapString:
                        if (count != 3) return DISP_E_BADPARAMCOUNT;
                        value = StrFunc.LCMapString(ToInt(Arg(args, count, 0), 0), ToInt(Arg(args, count, 1), 1), ToText(Arg(args, count, 2), 2));
                        break;
                    case Member.StrConv:
                        if (count is < 2 or > 3) return DISP_E_BADPARAMCOUNT;
                        value = StrFunc.StrConv(ToText(Arg(args, count, 0), 0), ToInt(Arg(args, count, 1), 1), count == 3 ? ToInt(Arg(args, count, 2), 2) : 0);
                        break;
                    case Member.Sprintf:
                        {
                            if (count < 1) return DISP_E_BADPARAMCOUNT;
                            string format = ToText(Arg(args, count, 0), 0);
                            var values = new object?[count - 1];
                            for (int i = 1; i < count; i++) values[i - 1] = ToObject(Arg(args, count, i));
                            value = CFormat.Format(format, values);
                            break;
                        }
                    case Member.Vsprintf:
                        if (count != 2) return DISP_E_BADPARAMCOUNT;
                        value = CFormat.Format(ToText(Arg(args, count, 0), 0), [ToObject(Arg(args, count, 1))]);
                        break;
                    case Member.GetDateFormat:
                    case Member.GetTimeFormat:
                        {
                            if (count != 4) return DISP_E_BADPARAMCOUNT;
                            int locale = ToInt(Arg(args, count, 0), 0), dateFlags = ToInt(Arg(args, count, 1), 1);
                            double date = ToDate(Arg(args, count, 2), 2);
                            string format = ToText(Arg(args, count, 3), 3);
                            value = (Member)id == Member.GetDateFormat
                                ? StrFunc.GetDateFormat(locale, dateFlags, date, format)
                                : StrFunc.GetTimeFormat(locale, dateFlags, date, format);
                            break;
                        }
                    default:
                        return DISP_E_MEMBERNOTFOUND;
                }
            }
        }
        catch (ArgumentFailure failure)
        {
            if (argError is not null) *argError = (uint)(count - 1 - failure.Index);
            return failure.HResult2;
        }
        if (result is not null) Marshal.GetNativeVariantForObject(value, (nint)result);
        return S_OK;
    }

    /// <summary>rgvarg は後ろの引数から並んでいる。index は先頭からの位置。</summary>
    private static unsafe Variant* Arg(Variant* args, int count, int index) => &args[count - 1 - index];

    private static unsafe Variant Change(Variant* source, int index, ushort type)
    {
        Variant changed = default;
        int hr = VariantChangeType(&changed, source, 0, type);
        if (hr < 0) throw new ArgumentFailure(index, hr == DISP_E_TYPEMISMATCH || hr == unchecked((int)0x80020008) ? DISP_E_TYPEMISMATCH : hr);
        return changed;
    }

    private static unsafe string ToText(Variant* source, int index)
    {
        var changed = Change(source, index, VT_BSTR);
        try { return Marshal.PtrToStringBSTR(changed.Data1) ?? ""; }
        finally { VariantClear(&changed); }
    }

    private static unsafe int ToInt(Variant* source, int index)
    {
        var changed = Change(source, index, VT_I4);
        return (int)changed.Data1;
    }

    private static unsafe double ToDate(Variant* source, int index)
    {
        var changed = Change(source, index, VT_DATE);
        return BitConverter.Int64BitsToDouble(changed.Data1);
    }

    /// <summary>可変長引数用。型を保ったまま .NET の値にする（参照渡しは値にする）。</summary>
    private static unsafe object? ToObject(Variant* source)
    {
        try
        {
            return Marshal.GetObjectForNativeVariant((nint)source);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or InvalidOleVariantTypeException or COMException)
        {
            return null;
        }
    }

    [DllImport("oleaut32", ExactSpelling = true)]
    private static extern unsafe int VariantChangeType(Variant* destination, Variant* source, ushort flags, ushort type);

    [DllImport("oleaut32", ExactSpelling = true)]
    private static extern unsafe int VariantClear(Variant* variant);

    /// <summary>IDispatch の vtable を持つ ComWrappers。</summary>
    private sealed unsafe class Wrappers : ComWrappers
    {
        public static readonly Wrappers Instance = new();
        private static readonly Guid IID_IDispatch = new("00020400-0000-0000-C000-000000000046");
        private static readonly ComInterfaceEntry* Entries = CreateEntries();

        private static ComInterfaceEntry* CreateEntries()
        {
            GetIUnknownImpl(out nint queryInterface, out nint addRef, out nint release);
            var vtable = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(Wrappers), sizeof(nint) * 7);
            vtable[0] = queryInterface;
            vtable[1] = addRef;
            vtable[2] = release;
            vtable[3] = (nint)(delegate* unmanaged<nint, uint*, int>)&GetTypeInfoCount;
            vtable[4] = (nint)(delegate* unmanaged<nint, uint, uint, nint*, int>)&GetTypeInfo;
            vtable[5] = (nint)(delegate* unmanaged<nint, Guid*, char**, uint, uint, int*, int>)&GetIDsOfNames;
            vtable[6] = (nint)(delegate* unmanaged<nint, int, Guid*, uint, ushort, DispParams*, Variant*, nint, uint*, int>)&InvokeEntry;
            var entries = (ComInterfaceEntry*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(Wrappers), sizeof(ComInterfaceEntry));
            entries[0].IID = IID_IDispatch;
            entries[0].Vtable = (nint)vtable;
            return entries;
        }

        protected override ComInterfaceEntry* ComputeVtables(object obj, CreateComInterfaceFlags flags, out int count)
        {
            count = 1;
            return Entries;
        }

        protected override object? CreateObject(nint externalComObject, CreateObjectFlags flags) => throw new NotSupportedException();

        protected override void ReleaseObjects(IEnumerable objects) => throw new NotSupportedException();

        [UnmanagedCallersOnly]
        private static int GetTypeInfoCount(nint self, uint* count)
        {
            *count = 0;
            return S_OK;
        }

        [UnmanagedCallersOnly]
        private static int GetTypeInfo(nint self, uint index, uint lcid, nint* info)
        {
            *info = 0;
            return DISP_E_BADINDEX;
        }

        [UnmanagedCallersOnly]
        private static int GetIDsOfNames(nint self, Guid* iid, char** names, uint count, uint lcid, int* ids)
        {
            int hr = S_OK;
            for (uint i = 0; i < count; i++)
            {
                string? name = new(names[i]);
                if (i == 0 && Ids.TryGetValue(name, out int id))
                {
                    ids[i] = id;
                }
                else
                {
                    ids[i] = -1;
                    hr = DISP_E_UNKNOWNNAME;
                }
            }
            return hr;
        }

        [UnmanagedCallersOnly]
        private static int InvokeEntry(nint self, int id, Guid* iid, uint lcid, ushort flags, DispParams* parameters, Variant* result, nint exception, uint* argError)
        {
            try
            {
                var target = ComInterfaceDispatch.GetInstance<StrFuncObject>((ComInterfaceDispatch*)self);
                return target.Invoke(id, flags, parameters, result, argError);
            }
            catch
            {
                return E_FAIL;
            }
        }
    }
}
