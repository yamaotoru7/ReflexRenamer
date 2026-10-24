using System.Runtime.InteropServices;

namespace FlexibleRenamer.Core.Tags;

/// <summary>Windows のプロパティシステムから読んだ1項目（表示名と表示用の値）。</summary>
public sealed record FileProperty(string Name, string CanonicalName, string Value);

/// <summary>
/// ファイルのプロパティ（エクスプローラーの「詳細」タブと同じもの）を読む。
/// タグ・リネームの「ファイルプロパティ情報を使う」で、ID3 や EXIF の読み取りの代わりに使う。
/// </summary>
public static class PropertyStoreReader
{
    private static readonly Guid IidPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly Guid IidPropertyDescription = new("6F79D558-3E96-4549-A1D1-7D75D2288814");

    /// <summary>プロパティストアを開けるか。壊れた mp3 などではプロパティハンドラが失敗する。</summary>
    public static bool CanOpen(string path)
    {
        var iid = IidPropertyStore;
        if (SHGetPropertyStoreFromParsingName(path, 0, 0, ref iid, out var store) != 0 || store is null)
        {
            return false;
        }
        Marshal.ReleaseComObject(store);
        return true;
    }

    public static List<FileProperty> Read(string path)
    {
        var result = new List<FileProperty>();
        var iid = IidPropertyStore;
        if (SHGetPropertyStoreFromParsingName(path, 0, 0, ref iid, out var store) != 0 || store is null)
        {
            return result;
        }
        try
        {
            store.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                if (store.GetAt(i, out var key) != 0)
                {
                    continue;
                }
                var descriptionIid = IidPropertyDescription;
                if (PSGetPropertyDescription(ref key, ref descriptionIid, out var description) != 0 || description is null)
                {
                    continue;
                }
                try
                {
                    description.GetDisplayName(out nint namePtr);
                    description.GetCanonicalName(out nint canonicalPtr);
                    string name = FreeString(namePtr);
                    string canonical = FreeString(canonicalPtr);
                    if (name.Length == 0)
                    {
                        continue;
                    }
                    var value = new PropVariant();
                    if (store.GetValue(ref key, out value) != 0)
                    {
                        continue;
                    }
                    try
                    {
                        if (description.FormatForDisplay(ref value, 0, out nint textPtr) != 0)
                        {
                            continue;
                        }
                        string text = FreeString(textPtr);
                        if (text.Length > 0)
                        {
                            result.Add(new FileProperty(name, canonical, text));
                        }
                    }
                    finally
                    {
                        PropVariantClear(ref value);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(description);
                }
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
        return result;
    }

    private static string FreeString(nint ptr)
    {
        if (ptr == 0)
        {
            return "";
        }
        string s = Marshal.PtrToStringUni(ptr) ?? "";
        Marshal.FreeCoTaskMem(ptr);
        return s;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;
    }

    /// <summary>PROPVARIANT（16 バイト）。中身は FormatForDisplay に渡すだけなので解釈しない。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PropVariant
    {
        public ushort Type;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public nint Pointer;
        public nint Pointer2;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetAt(uint index, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    [ComImport]
    [Guid("6F79D558-3E96-4549-A1D1-7D75D2288814")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyDescription
    {
        [PreserveSig] int GetPropertyKey(out PropertyKey key);
        [PreserveSig] int GetCanonicalName(out nint name);
        [PreserveSig] int GetPropertyType(out ushort type);
        [PreserveSig] int GetDisplayName(out nint name);
        [PreserveSig] int GetEditInvitation(out nint invitation);
        [PreserveSig] int GetTypeFlags(uint mask, out uint flags);
        [PreserveSig] int GetViewFlags(out uint flags);
        [PreserveSig] int GetDefaultColumnWidth(out uint characters);
        [PreserveSig] int GetDisplayType(out uint type);
        [PreserveSig] int GetColumnState(out uint flags);
        [PreserveSig] int GetGroupingRange(out uint range);
        [PreserveSig] int GetRelativeDescriptionType(out uint type);
        [PreserveSig] int GetRelativeDescription(ref PropVariant a, ref PropVariant b, out nint descA, out nint descB);
        [PreserveSig] int GetSortDescription(out uint description);
        [PreserveSig] int GetSortDescriptionLabel([MarshalAs(UnmanagedType.Bool)] bool descending, out nint label);
        [PreserveSig] int GetAggregationType(out uint type);
        [PreserveSig] int GetConditionType(out uint type, out uint operation);
        [PreserveSig] int GetEnumTypeList(ref Guid riid, out nint list);
        [PreserveSig] int CoerceToCanonicalValue(ref PropVariant value);
        [PreserveSig] int FormatForDisplay(ref PropVariant value, uint flags, out nint text);
        [PreserveSig] int IsValueCanonical(ref PropVariant value);
    }

    [DllImport("shell32", CharSet = CharSet.Unicode)]
    private static extern int SHGetPropertyStoreFromParsingName(string path, nint bindContext, uint flags, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? store);

    [DllImport("propsys")]
    private static extern int PSGetPropertyDescription(ref PropertyKey key, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyDescription? description);

    [DllImport("ole32")]
    private static extern int PropVariantClear(ref PropVariant value);
}
