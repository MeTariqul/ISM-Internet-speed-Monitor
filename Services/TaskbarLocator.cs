namespace SpeedMonitor.Services;

/// <summary>Rectangle in device pixels.</summary>
public readonly record struct RectPx(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public bool IntersectsWith(RectPx other)
        => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}

/// <summary>
/// Finds the Windows taskbar and its notification area (the tray).
///
/// Windows 11 can replace or momentarily hide the shell windows (auto-hide, Explorer restart,
/// mixed-DPI docks). Every call here is defensive: a missing taskbar returns false with an
/// empty rect so the caller can fall back to a floating widget instead of throwing.
/// </summary>
public static class TaskbarLocator
{
    private const string TaskbarClass = "Shell_TrayWnd";
    private const string TrayClass = "TrayNotifyWnd";
    private const int FallbackTrayWidth = 220;

    /// <summary>Taskbar handle, or <see cref="IntPtr.Zero"/> when the shell is not available.</summary>
    public static IntPtr FindTaskbar() => NativeMethods.TryFindWindow(TaskbarClass, null);

    /// <summary>Taskbar handle plus its on-screen rectangle, in one defensive call.</summary>
    public static bool TryGetTaskbarRect(out IntPtr taskbar, out RectPx rect)
    {
        taskbar = FindTaskbar();
        return TryGetRect(taskbar, out rect);
    }

    public static bool TryGetRect(IntPtr hwnd, out RectPx rect)
    {
        rect = default;
        if (!NativeMethods.TryGetWindowRect(hwnd, out NativeMethods.Rect r)) return false;

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

        try
        {
            NativeMethods.EnumChildWindowsProc callback = (child, _) =>
            {
                if (string.Equals(
                        NativeMethods.TryGetClassName(child), TrayClass, StringComparison.Ordinal))
                {
                    tray = child;
                    return false; // stop enumerating
                }

                return true;
            };

            NativeMethods.TryEnumChildWindows(taskbar, callback);
            GC.KeepAlive(callback);
        }
        catch
        {
            // Explorer is restarting or the taskbar is gone; use the geometric fallback below.
            tray = IntPtr.Zero;
        }

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
