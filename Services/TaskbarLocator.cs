using System.Runtime.InteropServices;
using System.Text;

namespace SpeedMonitor.Services;

/// <summary>Rectangle in device pixels.</summary>
public readonly record struct RectPx(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public bool IntersectsWith(RectPx other)
        => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}

/// <summary>Finds the Windows taskbar and its notification area (the tray).</summary>
public static class TaskbarLocator
{
    private const string TaskbarClass = "Shell_TrayWnd";
    private const string TrayClass = "TrayNotifyWnd";
    private const int FallbackTrayWidth = 220;

    public static IntPtr FindTaskbar() => NativeMethods.FindWindow(TaskbarClass, null);

    public static bool TryGetRect(IntPtr hwnd, out RectPx rect)
    {
        rect = default;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out NativeMethods.Rect r)) return false;
        rect = new RectPx(r.Left, r.Top, r.Right, r.Bottom);
        return rect.Width > 0 && rect.Height > 0;
    }

    /// <summary>
    /// Rectangle of the notification area inside the taskbar; the text is drawn just to the left of it.
    /// Falls back to a typical tray width when the tray window cannot be found.
    /// </summary>
    public static RectPx TrayRect(IntPtr taskbar, RectPx taskbarRect)
    {
        IntPtr tray = IntPtr.Zero;

        NativeMethods.EnumChildWindowsProc callback = (child, _) =>
        {
            var buffer = new StringBuilder(64);
            NativeMethods.GetClassName(child, buffer, buffer.Capacity);
            if (string.Equals(buffer.ToString(), TrayClass, StringComparison.Ordinal))
            {
                tray = child;
                return false; // stop enumerating
            }
            return true;
        };

        NativeMethods.EnumChildWindows(taskbar, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        if (tray != IntPtr.Zero && TryGetRect(tray, out RectPx rect) && rect.Width > 0)
        {
            return rect;
        }

        return new RectPx(
            taskbarRect.Right - FallbackTrayWidth,
            taskbarRect.Top,
            taskbarRect.Right,
            taskbarRect.Bottom);
    }
}
