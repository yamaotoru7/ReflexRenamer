using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ReflexRenamer.Core.Scripting;

/// <summary>スクリプトの言語。</summary>
public enum ScriptLanguage
{
    VBScript,
    JScript,
}

/// <summary>スクリプトの構文エラーや実行時エラー。</summary>
public sealed class ScriptException(string message) : Exception(message);

/// <summary>
/// Windows の ActiveScript エンジン（vbscript.dll / jscript.dll）で式を評価する。
/// オリジナル版の「スクリプト」機能と同じく、関数定義（Script.Preset.vbs やユーザーのスクリプト）を読み込んだうえで、
/// 置換欄の内容を式として評価する。
/// </summary>
/// <remarks>
/// エンジンはスレッドに結び付くので、専用の STA スレッドで生成と呼び出しをすべて行う。
/// </remarks>
public sealed class ScriptHost : IDisposable
{
    private static readonly Guid VBScriptClsid = new("B54F3741-5B07-11cf-A4B0-00AA004A55E8");
    private static readonly Guid JScriptClsid = new("F414C260-6AC0-11CF-B6D1-00AA00BBBB58");

    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;
    private IActiveScript? _engine;
    private IActiveScriptParse64? _parser;
    private readonly Site _site = new();

    public ScriptHost(ScriptLanguage language)
    {
        Language = language;
        _thread = new Thread(Loop) { IsBackground = true, Name = "ScriptHost" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        Invoke(() =>
        {
            var type = Type.GetTypeFromCLSID(language == ScriptLanguage.VBScript ? VBScriptClsid : JScriptClsid, throwOnError: true)!;
            _engine = (IActiveScript)Activator.CreateInstance(type)!;
            _parser = (IActiveScriptParse64)_engine;
            _engine.SetScriptSite(_site);
            _parser.InitNew();
            _engine.SetScriptState(ScriptState.Started);
            _engine.AddNamedItem(StrFuncItem, ScriptItem.IsVisible);
            _parser.ParseScriptText(language == ScriptLanguage.VBScript ? VBScriptPrelude : JScriptPrelude, null, null, null, 0, 0, ScriptText.IsVisible, out _, out _);
            return 0;
        });
    }

    /// <summary>StrFunc.Wrapper 互換オブジェクトの名前付き項目。スクリプトの識別子と重ならない名前にする。</summary>
    private const string StrFuncItem = "ReflexRenamerStrFuncWrapper";

    /// <summary>
    /// 原本の StrFunc.dll は 32bit COM で読めないので、CreateObject("StrFunc.Wrapper") を差し替えて互換オブジェクトを返す。
    /// ほかの ProgID は GetObject("", ProgID)（新しいインスタンスを作る）に回す。
    /// </summary>
    private const string VBScriptPrelude = """
        Function CreateObject(ProgID)
        	If LCase(ProgID) = "strfunc.wrapper" Then
        		Set CreateObject = ReflexRenamerStrFuncWrapper
        	Else
        		Set CreateObject = GetObject("", ProgID)
        	End If
        End Function
        """;

    /// <summary>JScript では new ActiveXObject("StrFunc.Wrapper") を差し替える。関数一覧に出ないよう無名関数で書く。</summary>
    private const string JScriptPrelude = """
        ActiveXObject = (function (create, wrapper) {
        	return function (progId, server) {
        		if (String(progId).toLowerCase() == "strfunc.wrapper") return wrapper;
        		return arguments.length > 1 ? new create(progId, server) : new create(progId);
        	};
        })(ActiveXObject, ReflexRenamerStrFuncWrapper);
        """;

    /// <summary>前置きのスクリプトで定義した関数。関数一覧には出さない。</summary>
    private static readonly string[] PreludeFunctions = ["CreateObject"];

    public ScriptLanguage Language { get; }

    /// <summary>関数定義などのスクリプトを読み込む。</summary>
    public void AddCode(string code) => Invoke(() =>
    {
        _site.LastError = null;
        _parser!.ParseScriptText(code, null, null, null, 0, 0, ScriptText.IsVisible, out _, out _);
        ThrowIfError();
        return 0;
    });

    /// <summary>式を評価し、結果を文字列で返す。</summary>
    public string Evaluate(string expression) => Invoke(() =>
    {
        _site.LastError = null;
        _parser!.ParseScriptText(expression, null, null, null, 0, 0, ScriptText.IsExpression, out object? result, out _);
        ThrowIfError();
        return Convert.ToString(result, System.Globalization.CultureInfo.CurrentCulture) ?? "";
    });

    /// <summary>エンジンの型情報から、公開されている関数を宣言順に取得する。</summary>
    public IReadOnlyList<string> FunctionNames() => Invoke(() =>
    {
        _engine!.GetScriptDispatch(null, out object dispatch);
        ITypeInfo? info = null;
        try
        {
            ((IScriptDispatch)dispatch).GetTypeInfo(0, 0, out info);
            info.GetTypeAttr(out nint attrPointer);
            try
            {
                var attr = Marshal.PtrToStructure<TYPEATTR>(attrPointer);
                var names = new List<string>();
                for (int i = 0; i < attr.cFuncs; i++)
                {
                    info.GetFuncDesc(i, out nint descriptor);
                    try
                    {
                        var function = Marshal.PtrToStructure<FUNCDESC>(descriptor);
                        if (function.invkind != INVOKEKIND.INVOKE_FUNC) continue;
                        info.GetDocumentation(function.memid, out string name, out _, out _, out _);
                        if (Language == ScriptLanguage.VBScript && PreludeFunctions.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                        names.Add(name);
                    }
                    finally { info.ReleaseFuncDesc(descriptor); }
                }
                return (IReadOnlyList<string>)names;
            }
            finally { info.ReleaseTypeAttr(attrPointer); }
        }
        finally
        {
            if (info is not null) Marshal.ReleaseComObject(info);
            Marshal.ReleaseComObject(dispatch);
        }
    });

    private void ThrowIfError()
    {
        if (_site.LastError is { } error)
        {
            _site.LastError = null;
            throw new ScriptException(error);
        }
    }

    public void Dispose()
    {
        if (_queue.IsAddingCompleted)
        {
            return;
        }
        Invoke(() =>
        {
            _engine?.Close();
            if (_engine is not null)
            {
                Marshal.FinalReleaseComObject(_engine);
            }
            _engine = null;
            _parser = null;
            return 0;
        });
        _queue.CompleteAdding();
        _thread.Join();
    }

    private void Loop()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            action();
        }
    }

    private T Invoke<T>(Func<T> func)
    {
        T result = default!;
        Exception? error = null;
        using var done = new ManualResetEventSlim();
        _queue.Add(() =>
        {
            try
            {
                result = func();
            }
            catch (COMException e)
            {
                error = _site.LastError is { } message ? new ScriptException(message) : new ScriptException(e.Message);
                _site.LastError = null;
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                done.Set();
            }
        });
        done.Wait();
        if (error is not null)
        {
            throw error;
        }
        return result;
    }

    private static class ScriptState
    {
        public const uint Started = 1;
    }

    private static class ScriptItem
    {
        public const uint IsVisible = 0x2;
    }

    private static class ScriptText
    {
        public const uint IsVisible = 0x2;
        public const uint IsExpression = 0x20;
    }

    /// <summary>エンジンからの通知を受ける。エラーの内容を覚えておく。</summary>
    private sealed class Site : IActiveScriptSite
    {
        public string? LastError { get; set; }

        public int GetLCID(out uint lcid)
        {
            lcid = 0;
            return unchecked((int)0x80004001); // E_NOTIMPL：既定のロケールを使う
        }

        private readonly StrFuncObject _strFunc = new();

        /// <remarks>エンジンは要らない方の出力先に NULL を渡すので、ポインターのまま受け取る。</remarks>
        public unsafe int GetItemInfo(string name, uint returnMask, nint* item, nint* typeInfo)
        {
            if (item is not null) *item = 0;
            if (typeInfo is not null) *typeInfo = 0;
            if (!string.Equals(name, StrFuncItem, StringComparison.OrdinalIgnoreCase))
            {
                return unchecked((int)0x8002802B); // TYPE_E_ELEMENTNOTFOUND
            }
            if ((returnMask & 0x1) != 0 && item is not null) // SCRIPTINFO_IUNKNOWN
            {
                *item = _strFunc.CreateComPointer();
            }
            return 0;
        }

        public int GetDocVersionString(out string? version)
        {
            version = null;
            return unchecked((int)0x80004001);
        }

        public int OnScriptTerminate(nint result, nint exceptionInfo) => 0;

        public int OnStateChange(uint state) => 0;

        public int OnScriptError(IActiveScriptError error)
        {
            error.GetExceptionInfo(out var info);
            error.GetSourcePosition(out _, out uint line, out int column);
            string description = string.IsNullOrEmpty(info.bstrDescription) ? $"エラー 0x{info.scode:X8}" : info.bstrDescription;
            LastError = $"{description}（{line + 1}行 {column + 1}文字目）";
            return 0;
        }

        public int OnEnterScript() => 0;

        public int OnLeaveScript() => 0;
    }
}

[ComImport]
[Guid("00020400-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IScriptDispatch
{
    void GetTypeInfoCount(out uint count);
    void GetTypeInfo(uint index, uint lcid, out ITypeInfo info);
}

[ComImport]
[Guid("BB1A2AE1-A4F9-11cf-8F20-00805F2CD064")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActiveScript
{
    void SetScriptSite(IActiveScriptSite site);
    void GetScriptSite(ref Guid riid, out nint site);
    void SetScriptState(uint state);
    void GetScriptState(out uint state);
    void Close();
    void AddNamedItem([MarshalAs(UnmanagedType.LPWStr)] string name, uint flags);
    void AddTypeLib(ref Guid typeLib, uint major, uint minor, uint flags);
    void GetScriptDispatch([MarshalAs(UnmanagedType.LPWStr)] string? itemName, [MarshalAs(UnmanagedType.IDispatch)] out object dispatch);
    void GetCurrentScriptThreadID(out uint id);
    void GetScriptThreadID(uint win32ThreadId, out uint id);
    void GetScriptThreadState(uint id, out uint state);
    void InterruptScriptThread(uint id, nint exceptionInfo, uint flags);
    void Clone(out IActiveScript script);
}

[ComImport]
[Guid("C7EF7658-E1EE-480E-97EA-D52CB4D76D17")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActiveScriptParse64
{
    void InitNew();

    void AddScriptlet(
        [MarshalAs(UnmanagedType.LPWStr)] string defaultName,
        [MarshalAs(UnmanagedType.LPWStr)] string code,
        [MarshalAs(UnmanagedType.LPWStr)] string itemName,
        [MarshalAs(UnmanagedType.LPWStr)] string subItemName,
        [MarshalAs(UnmanagedType.LPWStr)] string eventName,
        [MarshalAs(UnmanagedType.LPWStr)] string delimiter,
        ulong sourceContextCookie,
        uint startingLineNumber,
        uint flags,
        [MarshalAs(UnmanagedType.BStr)] out string name,
        out EXCEPINFO exceptionInfo);

    void ParseScriptText(
        [MarshalAs(UnmanagedType.LPWStr)] string code,
        [MarshalAs(UnmanagedType.LPWStr)] string? itemName,
        [MarshalAs(UnmanagedType.IUnknown)] object? context,
        [MarshalAs(UnmanagedType.LPWStr)] string? delimiter,
        ulong sourceContextCookie,
        uint startingLineNumber,
        uint flags,
        out object? result,
        out EXCEPINFO exceptionInfo);
}

[ComImport]
[Guid("DB01A1E3-A42B-11cf-8F20-00805F2CD064")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActiveScriptSite
{
    [PreserveSig]
    int GetLCID(out uint lcid);

    [PreserveSig]
    unsafe int GetItemInfo([MarshalAs(UnmanagedType.LPWStr)] string name, uint returnMask, nint* item, nint* typeInfo);

    [PreserveSig]
    int GetDocVersionString([MarshalAs(UnmanagedType.BStr)] out string? version);

    [PreserveSig]
    int OnScriptTerminate(nint result, nint exceptionInfo);

    [PreserveSig]
    int OnStateChange(uint state);

    [PreserveSig]
    int OnScriptError(IActiveScriptError error);

    [PreserveSig]
    int OnEnterScript();

    [PreserveSig]
    int OnLeaveScript();
}

[ComImport]
[Guid("EAE1BA61-A4ED-11cf-8F20-00805F2CD064")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActiveScriptError
{
    void GetExceptionInfo(out EXCEPINFO exceptionInfo);
    void GetSourcePosition(out uint sourceContext, out uint lineNumber, out int characterPosition);
    void GetSourceLineText([MarshalAs(UnmanagedType.BStr)] out string line);
}
