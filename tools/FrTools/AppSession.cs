using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace FrTools;

internal enum AppKind
{
    Original,
    Clone,
}

/// <summary>
/// オリジナル版または移植版を起動し、UI Automation で操作するセッション。
/// </summary>
internal sealed class AppSession : IDisposable
{
    /// <summary>スクリーンショットを比較しやすいよう、メインウィンドウはこの位置と大きさに揃える。</summary>
    public static readonly Rectangle MainBounds = new(40, 40, 1100, 760);

    private AppSession(AppKind kind, Application app, UIA3Automation automation, Window main)
    {
        Kind = kind;
        App = app;
        Automation = automation;
        Main = main;
    }

    public AppKind Kind { get; }
    public Application App { get; }
    public UIA3Automation Automation { get; }
    public Window Main { get; private set; }

    public static AppSession Launch(AppKind kind, string? folder, IReadOnlyDictionary<string, Dictionary<string, string>>? settings = null)
    {
        string exe = kind == AppKind.Original ? Sandbox.OriginalExe : Sandbox.CloneExe;
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException(kind == AppKind.Original
                ? "オリジナル版の作業コピーがない。先に `sandbox` を実行する"
                : "移植版がビルドされていない。先に `dotnet build` を実行する", exe);
        }

        KillExisting(exe);
        if (kind == AppKind.Clone)
        {
            // 移植版も設定とログを実行ディレクトリに書くので、オリジナル版と同じく毎回まっさらな状態で起動する
            foreach (var stateFile in new[] { Sandbox.IniName(kind), "Rename.log" })
            {
                File.Delete(Path.Combine(Path.GetDirectoryName(exe)!, stateFile));
            }
        }

        if (settings is not null)
        {
            // 起動前に設定を書いておく（言語など、起動時にしか反映されない設定のため）。ini は BOM 付き UTF-16 LE
            var text = new System.Text.StringBuilder();
            foreach (var (section, values) in settings)
            {
                text.Append('[').Append(section).Append("]").Append("\r\n");
                foreach (var (key, value) in values)
                {
                    text.Append(key).Append('=').Append(value).Append("\r\n");
                }
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(exe)!, Sandbox.IniName(kind)), text.ToString(), new System.Text.UnicodeEncoding(false, true));
        }

        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
        };
        if (folder is not null)
        {
            psi.ArgumentList.Add(folder);
        }

        var app = Application.Launch(psi);
        var automation = new UIA3Automation();
        var main = app.GetMainWindow(automation, TimeSpan.FromSeconds(20))
            ?? throw new TimeoutException("メインウィンドウが表示されない");

        var session = new AppSession(kind, app, automation, main);
        session.Normalize();
        return session;
    }

    /// <summary>メインウィンドウを前面に出し、決まった位置と大きさにする。</summary>
    public void Normalize()
    {
        var hwnd = Main.Properties.NativeWindowHandle.Value;
        ShowWindow(hwnd, 9); // SW_RESTORE
        SetWindowPos(hwnd, 0, MainBounds.X, MainBounds.Y, MainBounds.Width, MainBounds.Height, 0x0040);
        EnsureForeground();
        Settle();
    }

    /// <summary>
    /// このアプリのウィンドウを前面に出す。
    /// 前面にないままマウスやキーボードを操作すると、入力が別のアプリ（ユーザーのターミナルなど）に届いてしまう。
    /// ユーザーが PC を操作していれば手が止まるのを待ってから前面に出す。どうしても出せなければ例外にする。
    /// </summary>
    public void EnsureForeground()
    {
        int pid = App.ProcessId;
        // 画面ロックや全画面アプリで前面に出せないことがあるので、2分までは待ちながら試す
        var deadline = DateTime.UtcNow.AddMinutes(2);
        bool announced = false;
        for (int round = 0; DateTime.UtcNow < deadline; round++)
        {
            if (ForegroundProcessId() == pid)
            {
                return;
            }
            if (round >= 20 && !announced)
            {
                Console.WriteLine("  アプリを前面に出せないので待つ（画面ロック？）...");
                announced = true;
            }
            WaitForUserIdle();

            // 別プロセスが前面のときは SetForegroundWindow が拒否されるので、手段を変えながら試す
            var hwnd = ActiveWindowOfApp();
            switch (round % 4)
            {
                case 0:
                    // Alt キーの入力で制限を外してから呼ぶ
                    keybd_event(0x12, 0, 0, 0);
                    keybd_event(0x12, 0, 2, 0);
                    ShowWindow(hwnd, 9);
                    SetForegroundWindow(hwnd);
                    if (ForegroundProcessId() != pid)
                    {
                        // それでも駄目なら、自分のウィンドウの害のない場所（ステータスバーの左端）を実際にクリックして前面にする
                        ClickInside(hwnd);
                    }
                    break;
                case 1:
                {
                    // 前面のスレッドに入力を結び付けて呼ぶ
                    uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
                    uint me = GetCurrentThreadId();
                    bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
                    BringWindowToTop(hwnd);
                    SetForegroundWindow(hwnd);
                    if (attached)
                    {
                        AttachThreadInput(me, fgThread, false);
                    }
                    break;
                }
                case 2:
                    SwitchToThisWindow(hwnd, true);
                    break;
                default:
                    // 最小化して戻すと前面に来る
                    ShowWindow(hwnd, 6);
                    ShowWindow(hwnd, 9);
                    break;
            }
            MarkOwnInput();
            Thread.Sleep(300);
        }
        throw new InvalidOperationException($"アプリを前面に出せない（前面: {DescribeForeground()}）。入力が他のウィンドウに届くのを防ぐため中止した");
    }

    /// <summary>ウィンドウの左下（ステータスバー付近）をマウスでクリックする。実際の入力なので前面の制限を受けない。</summary>
    private static void ClickInside(nint hwnd)
    {
        if (!GetWindowRect(hwnd, out var r))
        {
            return;
        }
        int x = r.Left + Math.Min(60, (r.Right - r.Left) / 2);
        int y = r.Bottom - 12;
        FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point(x, y));
        FlaUI.Core.Input.Mouse.Click(FlaUI.Core.Input.MouseButton.Left);
        Thread.Sleep(200);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32")]
    private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);

    private static string DescribeForeground()
    {
        var hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out int pid);
        string name = "?";
        try
        {
            name = Process.GetProcessById(pid).ProcessName;
        }
        catch (ArgumentException)
        {
        }
        return $"{name} pid={pid}";
    }

    /// <summary>このツールが最後に入力を送り終えた時刻（Environment.TickCount）。</summary>
    private static uint s_ownInputTick;

    /// <summary>このツールの入力が終わったことを記録する。これより後の入力はユーザーのものとみなす。</summary>
    public static void MarkOwnInput() => s_ownInputTick = (uint)Environment.TickCount;

    /// <summary>
    /// ユーザーが PC を操作していたら、3秒間入力がなくなるまで待つ（最長10分）。
    /// このツール自身の入力（<see cref="MarkOwnInput"/> 以前）は数えない。
    /// </summary>
    public static void WaitForUserIdle()
    {
        const uint idleMs = 3000;
        var deadline = DateTime.UtcNow.AddMinutes(10);
        bool announced = false;
        while (DateTime.UtcNow < deadline)
        {
            var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            GetLastInputInfo(ref info);
            uint now = (uint)Environment.TickCount;
            bool userInput = info.Time > s_ownInputTick + 100;
            if (!userInput || now - info.Time >= idleMs)
            {
                if (announced) Console.WriteLine("  操作が止まったので再開する");
                return;
            }
            if (!announced)
            {
                Console.WriteLine("  ユーザーが操作中なので待つ...");
                announced = true;
            }
            Thread.Sleep(500);
        }
        throw new TimeoutException("ユーザーの操作が10分続いたので中止した");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    /// <summary>モーダルダイアログが出ていればそれ、なければメインウィンドウ。</summary>
    private nint ActiveWindowOfApp()
    {
        var main = Main.Properties.NativeWindowHandle.Value;
        var popup = GetLastActivePopup(main);
        return popup != 0 ? popup : main;
    }

    private static int ForegroundProcessId()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out int pid);
        return pid;
    }

    /// <summary>このプロセスのトップレベルウィンドウ（メインとダイアログ）。</summary>
    public IReadOnlyList<Window> TopWindows() => App.GetAllTopLevelWindows(Automation);

    /// <summary>
    /// トップレベルウィンドウと、その直下にあるウィンドウ（モーダルダイアログなど）。
    /// UI Automation では所有ウィンドウがオーナーの子として現れるので、ダイアログを探すときはこちらを使う。
    /// </summary>
    public IReadOnlyList<AutomationElement> AllWindows()
    {
        var result = new List<AutomationElement>();
        foreach (var top in TopWindows())
        {
            result.Add(top);
            result.AddRange(top.FindAllChildren(cf => cf.ByControlType(ControlType.Window)));
        }
        return result;
    }

    /// <summary>UI の更新が落ち着くまで少し待つ。</summary>
    public void Settle(int ms = 400)
    {
        Thread.Sleep(ms);
        try
        {
            App.WaitWhileBusy(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // 応答待ちが取れなくても続行する
        }
    }

    public void Capture(AutomationElement element, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        EnsureForeground();
        Thread.Sleep(150);
        FlaUI.Core.Capturing.Capture.Element(element).ToFile(path);
    }

    /// <summary>WM_COMMAND を送る（オリジナル版のメニューコマンド ID を直接実行する）。</summary>
    public void PostCommand(int id)
    {
        PostMessage(Main.Properties.NativeWindowHandle.Value, 0x0111, id, 0);
        Settle();
    }

    public void Dispose()
    {
        try
        {
            App.Close();
            if (!App.HasExited)
            {
                App.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(1));
            }
        }
        catch (Exception)
        {
        }
        if (!App.HasExited)
        {
            App.Kill();
        }
        Automation.Dispose();
        App.Dispose();
    }

    public static void KillExisting(string exe)
    {
        string name = Path.GetFileNameWithoutExtension(exe);
        foreach (var p in Process.GetProcessesByName(name))
        {
            try
            {
                // 同じ名前でも、作業コピー以外で起動しているユーザーのプロセスは終了しない。
                if (!string.Equals(p.MainModule?.FileName, Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                p.Kill();
                p.WaitForExit(3000);
            }
            catch (Exception)
            {
            }
            finally
            {
                p.Dispose();
            }
        }
    }

    [DllImport("user32")]
    private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32")]
    private static extern bool ShowWindow(nint hwnd, int cmd);

    [DllImport("user32")]
    private static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out int pid);

    [DllImport("user32")]
    private static extern nint GetLastActivePopup(nint hwnd);

    [DllImport("user32")]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachFlag);

    [DllImport("user32")]
    private static extern bool BringWindowToTop(nint hwnd);

    [DllImport("user32")]
    private static extern void SwitchToThisWindow(nint hwnd, bool altTab);

    [DllImport("kernel32")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, nuint extra);

    [DllImport("user32")]
    private static extern bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);
}
