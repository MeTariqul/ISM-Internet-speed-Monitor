using System.Drawing;
using WinForms = System.Windows.Forms;

namespace SpeedMonitor.Services;

/// <summary>
/// Detects a full-screen foreground window on the same monitor as the overlay, so the
/// readout steps out of the way of videos, presentations and games and comes back when
/// the app leaves full screen.
///
/// The check is intentionally conservative: shell windows and minimised windows are never
/// treated as full screen, and the comparison uses the DWM visible frame so a plain
/// maximised window (which keeps the taskbar visible) does not hide the readout.
/// </summary>
public static class FullscreenDetector
{
    private static readonly string[] ShellClasses =
    {
        "Progman",              // desktop
        "WorkerW",              // wallpaper host on some Windows builds
        "Shell_TrayWnd",        // taskbar
        "Shell_SecondaryTrayWnd",
        "LockApp",              // lock screen
    };

    /// <summary>True when the current foreground window covers the overlay's monitor.</summary>
    public static bool IsForegroundFullscreen(IntPtr overlayHwnd) => Query(overlayHwnd) ?? false;

    /// <summary>
    /// True/false when another app's foreground window is (or is not) full screen, or
    /// <c>null</c> when our own menu/dialog is in front and the state cannot be judged.
    /// Callers keep their previous state for <c>null</c>, so opening the tray menu while a
    /// video is full screen does not flash the readout back on top of it.
    /// </summary>
    public static bool? Query(IntPtr overlayHwnd)
    {
        IntPtr foreground = NativeMethods.TryGetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == overlayHwnd) return null;
        if (NativeMethods.TryGetWindowProcessId(foreground) == (uint)Environment.ProcessId) return null;
        if (NativeMethods.TryIsIconic(foreground)) return false;
        if (IsShellWindow(foreground)) return false;

        if (!NativeMethods.TryGetVisibleWindowRect(foreground, out RectPx window)) return false;

        Rectangle monitor = MonitorBounds(foreground);
        if (overlayHwnd != IntPtr.Zero && MonitorBounds(overlayHwnd) != monitor) return false;

        return CoversMonitor(window, new RectPx(monitor.Left, monitor.Top, monitor.Right, monitor.Bottom));
    }

    /// <summary>True when <paramref name="window"/> covers <paramref name="monitor"/> within a small tolerance.</summary>
    public static bool CoversMonitor(RectPx window, RectPx monitor, int tolerancePx = 2)
        => window.Left <= monitor.Left + tolerancePx
        && window.Top <= monitor.Top + tolerancePx
        && window.Right >= monitor.Right - tolerancePx
        && window.Bottom >= monitor.Bottom - tolerancePx;

    private static bool IsShellWindow(IntPtr hwnd)
    {
        string className = NativeMethods.TryGetClassName(hwnd);
        foreach (string shellClass in ShellClasses)
        {
            if (string.Equals(className, shellClass, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static Rectangle MonitorBounds(IntPtr hwnd)
    {
        try
        {
            return WinForms.Screen.FromHandle(hwnd).Bounds;
        }
        catch
        {
            return WinForms.Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        }
    }
}
