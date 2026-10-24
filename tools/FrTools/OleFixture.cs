using System.Runtime.InteropServices;

namespace FrTools;

/// <summary>
/// Windows の構造化ストレージ（StgCreateStorageEx と IPropertySetStorage）で、文書情報（SummaryInformation /
/// DocumentSummaryInformation）だけを持つ本物の OLE 複合文書を作る（T405：オリジナル版に読ませて照合する）。
/// </summary>
internal static class OleFixture
{
    private static readonly Guid FmtidSummary = new("F29F85E0-4FF9-1068-AB91-08002B27B3D9");
    private static readonly Guid FmtidDocSummary = new("D5CDD502-2E9C-101B-9397-08002B2CF9AE");
    private static readonly Guid IidPropertySetStorage = new("0000013A-0000-0000-C000-000000000046");

    private const uint StgmCreate = 0x1000, StgmReadWrite = 0x2, StgmShareExclusive = 0x10;
    private const uint PropsetflagAnsi = 2;
    private const ushort VtLpstr = 30, VtFiletime = 64;

    public sealed record Info(string Title, string Subject, string Author, string Keywords, string Comments, string AppName, string Company, DateTime Created, DateTime LastSaved, bool UnicodeDocSummary = false);

    public static byte[] Build(Info info)
    {
        string path = Path.Combine(Path.GetTempPath(), "frtools-ole-" + Guid.NewGuid().ToString("N") + ".doc");
        try
        {
            Guid iid = IidPropertySetStorage;
            Marshal.ThrowExceptionForHR(StgCreateStorageEx(path, StgmCreate | StgmReadWrite | StgmShareExclusive, 0, 0, 0, 0, ref iid, out nint setStorage));
            try
            {
                Write(setStorage, FmtidSummary, PropsetflagAnsi,
                    (2, info.Title), (3, info.Subject), (4, info.Author), (5, info.Keywords), (6, info.Comments), (0x12, info.AppName),
                    (0x0C, info.Created), (0x0D, info.LastSaved));
                Write(setStorage, FmtidDocSummary, info.UnicodeDocSummary ? 0u : PropsetflagAnsi, (0x0F, info.Company));
            }
            finally
            {
                Release(setStorage);
            }
            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // COM の既定のマーシャリングは型ライブラリを要求して失敗するので、vtable を直接呼ぶ
    private delegate int CreateFn(nint self, ref Guid fmtid, nint clsid, uint flags, uint mode, out nint storage);
    private delegate int WriteMultipleFn(nint self, uint count, [In] PropSpec[] specs, [In] PropVariant[] values, uint firstNameId);
    private delegate int CommitFn(nint self, uint flags);
    private delegate uint ReleaseFn(nint self);

    private static T Method<T>(nint obj, int index) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), index * IntPtr.Size));

    private static void Release(nint obj) => Method<ReleaseFn>(obj, 2)(obj);

    private static void Write(nint setStorage, Guid fmtid, uint flags, params (uint Id, object Value)[] values)
    {
        // IPropertySetStorage::Create は vtable の 3 番目（IUnknown の 3 つの後）
        Marshal.ThrowExceptionForHR(Method<CreateFn>(setStorage, 3)(setStorage, ref fmtid, 0, flags, StgmCreate | StgmReadWrite | StgmShareExclusive, out nint storage));
        var specs = values.Select(v => new PropSpec { Kind = 1, Id = v.Id }).ToArray();
        var variants = new PropVariant[values.Length];
        var allocated = new List<nint>();
        try
        {
            for (int i = 0; i < values.Length; i++)
            {
                switch (values[i].Value)
                {
                    case string s:
                        nint p = Marshal.StringToHGlobalAnsi(s);
                        allocated.Add(p);
                        variants[i] = new PropVariant { Vt = VtLpstr, Value = p };
                        break;
                    case DateTime t:
                        variants[i] = new PropVariant { Vt = VtFiletime, Value = (nint)t.ToFileTimeUtc() };
                        break;
                }
            }
            // IPropertyStorage::WriteMultiple は 4 番目、Commit は 9 番目
            Marshal.ThrowExceptionForHR(Method<WriteMultipleFn>(storage, 4)(storage, (uint)specs.Length, specs, variants, 2));
            Marshal.ThrowExceptionForHR(Method<CommitFn>(storage, 9)(storage, 0));
        }
        finally
        {
            allocated.ForEach(Marshal.FreeHGlobal);
            Release(storage);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropSpec
    {
        public uint Kind;
        public nint IdOrName;

        public uint Id { readonly get => (uint)IdOrName; set => IdOrName = (nint)value; }
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort Vt;
        [FieldOffset(8)] public nint Value;
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int StgCreateStorageEx(string pwcsName, uint grfMode, uint stgfmt, uint grfAttrs, nint pStgOptions, nint reserved, ref Guid riid, out nint ppObjectOpen);
}
