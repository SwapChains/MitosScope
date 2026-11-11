using System;
using System.Runtime.InteropServices;

namespace MitosScope.App.Services;

/// <summary>
/// Event-driven (no polling) notification of foreground window changes, via
/// SetWinEventHook(EVENT_SYSTEM_FOREGROUND). Our own windows are skipped, so clicking a panel
/// in edit mode doesn't count as "focus left the game". Windows only for now.
/// Must be started on the UI thread (the hook callback is delivered through its message loop).
/// </summary>
public sealed partial class ForegroundWatcher : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private delegate void WinEventProc(nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    private WinEventProc? _proc; // kept in a field so the GC doesn't collect the callback
    private nint _hook;

    /// <summary>Raised on the UI thread with the PID that owns the new foreground window.</summary>
    public event Action<int>? ForegroundChanged;

    public static bool IsSupported => OperatingSystem.IsWindows();

    public void Start()
    {
        if (!IsSupported || _hook != 0)
            return;

        _proc = OnWinEvent;
        _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0,
            Marshal.GetFunctionPointerForDelegate(_proc), 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        Raise(GetForegroundWindow()); // initial state
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time) =>
        Raise(hwnd);

    private void Raise(nint hwnd)
    {
        if (hwnd == 0)
            return;
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid != 0 && pid != Environment.ProcessId)
            ForegroundChanged?.Invoke((int)pid);
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            UnhookWinEvent(_hook);
            _hook = 0;
        }
    }

    [LibraryImport("user32.dll")]
    private static partial nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc,
        nint lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWinEvent(nint hWinEventHook);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}
