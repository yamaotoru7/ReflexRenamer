using System.Runtime.InteropServices;

namespace ReflexRenamer.Ui;

/// <summary>Windows 7 以降のタスクバーの進行状況（ITaskbarList3）。オリジナル版 v8.3 の機能。</summary>
public static class TaskbarProgress
{
    private static ITaskbarList3? s_taskbar;

    private static ITaskbarList3? Taskbar
    {
        get
        {
            if (s_taskbar is null)
            {
                try
                {
                    s_taskbar = (ITaskbarList3)new TaskbarInstance();
                    s_taskbar.HrInit();
                }
                catch (Exception e) when (e is COMException or InvalidCastException)
                {
                    return null;
                }
            }
            return s_taskbar;
        }
    }

    public static void SetProgress(nint window, double ratio)
    {
        try
        {
            Taskbar?.SetProgressState(window, 2); // TBPF_NORMAL
            Taskbar?.SetProgressValue(window, (ulong)Math.Clamp(ratio * 1000, 0, 1000), 1000);
        }
        catch (COMException)
        {
        }
    }

    public static void Clear(nint window)
    {
        try
        {
            Taskbar?.SetProgressState(window, 0); // TBPF_NOPROGRESS
        }
        catch (COMException)
        {
        }
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarInstance
    {
    }

    [ComImport]
    [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(nint hwnd);
        void DeleteTab(nint hwnd);
        void ActivateTab(nint hwnd);
        void SetActiveAlt(nint hwnd);
        void MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(nint hwnd, ulong completed, ulong total);
        void SetProgressState(nint hwnd, int state);
    }
}
