using System.Runtime.InteropServices;
using System.Text;

namespace SpeedMonitor.Services;

internal static class NativeMethods
{
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>Places a window at the top of the topmost band.</summary>
    public static readonly IntPtr HwndTopmost = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    public delegate bool EnumChildWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // ------------------------------------------------------------------ safe wrappers
    // P/Invoke itself should not throw on Windows, but a torn-down or replaced shell window
    // handle can make a call fail in surprising ways. Every wrapper here swallows failures
    // and reports false / a neutral value so the overlay degrades instead of crashing Explorer.

    public static IntPtr TryFindWindow(string? className, string? windowName)
    {
        try
        {
            return FindWindow(className, windowName);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public static bool TryGetWindowRect(IntPtr hwnd, out Rect rect)
    {
        rect = default;
        if (hwnd == IntPtr.Zero) return false;

        try
        {
            return GetWindowRect(hwnd, out rect) && rect.Width > 0 && rect.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryIsWindowVisible(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        try
        {
            return IsWindowVisible(hwnd);
        }
        catch
        {
            return false;
        }
    }

    public static bool TrySetWindowPos(
        IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags)
    {
        if (hwnd == IntPtr.Zero) return false;

        try
        {
            return SetWindowPos(hwnd, insertAfter, x, y, cx, cy, flags);
        }
        catch
        {
            return false;
        }
    }

    public static string TryGetClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;

        try
        {
            var buffer = new StringBuilder(64);
            return GetClassName(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static bool TryEnumChildWindows(IntPtr parent, EnumChildWindowsProc callback)
    {
        if (parent == IntPtr.Zero) return false;

        try
        {
            return EnumChildWindows(parent, callback, IntPtr.Zero);
        }
        catch
        {
            return false;
        }
    }

    public static int TryGetWindowLong(IntPtr hwnd, int index)
    {
        if (hwnd == IntPtr.Zero) return 0;

        try
        {
            return GetWindowLong(hwnd, index);
        }
        catch
        {
            return 0;
        }
    }

    public static bool TrySetWindowLong(IntPtr hwnd, int index, int value)
    {
        if (hwnd == IntPtr.Zero) return false;

        try
        {
            SetWindowLong(hwnd, index, value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
