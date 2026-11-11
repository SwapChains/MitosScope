using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace MitosScope.App.Overlay;

/// <summary>
/// Platform-specific window tweaks that Avalonia doesn't expose. Windows only for now; on other
/// platforms the calls are no-ops (macOS: NSWindow.ignoresMouseEvents, X11: input shape — later).
/// </summary>
internal static partial class WindowInterop
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_LAYERED = 0x00080000;
    private const long WS_EX_NOACTIVATE = 0x08000000;
    private const uint LWA_ALPHA = 0x2;

    /// <summary>
    /// Click-through: mouse input goes to whatever is below (the game). Also never takes focus
    /// and stays out of Alt+Tab.
    /// </summary>
    public static void SetClickThrough(Window window, bool enabled)
    {
        if (!OperatingSystem.IsWindows() || window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == 0)
            return;

        var style = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        var newStyle = style | WS_EX_TOOLWINDOW;
        newStyle = enabled
            ? newStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE
            : newStyle & ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);

        if (newStyle == style)
            return;

        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)newStyle);

        // A window that just became layered isn't drawn until its attributes are set.
        if ((style & WS_EX_LAYERED) == 0 && (newStyle & WS_EX_LAYERED) != 0)
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);
}
