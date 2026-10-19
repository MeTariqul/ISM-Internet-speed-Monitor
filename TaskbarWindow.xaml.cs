using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using SpeedMonitor.Services;
using SpeedMonitor.ViewModels;

namespace SpeedMonitor;

/// <summary>
/// A borderless, non-activating overlay that paints the live speeds over the taskbar,
/// just to the left of the notification area. Repositions itself as the taskbar moves,
/// resizes or auto-hides.
/// </summary>
public partial class TaskbarWindow : Window
{
    private const int GapToTrayPx = 8;
    private const int EdgePaddingPx = 4;
    private const int FallbackHeightDip = 30;
    private const int FallbackMarginPx = 12;
    private static readonly TimeSpan ThemeSampleInterval = TimeSpan.FromSeconds(2);

    private static readonly Brush LightText = Frozen(0xF2, 0xF5, 0xF7);
    private static readonly Brush DarkText = Frozen(0x11, 0x14, 0x18);

    private readonly MonitorViewModel _viewModel;
    private readonly DispatcherTimer _timer;

    private IntPtr _hwnd;
    private bool _ready;
    private bool _userWantsVisible = true;
    private DateTime _lastThemeSample = DateTime.MinValue;
    private bool _darkTextOnLightBar;
    private bool _repositioning;

    /// <summary>Raised when the overlay is clicked; the controller decides which menu to show.</summary>
    public event EventHandler? MenuRequested;

    public TaskbarWindow(MonitorViewModel viewModel, bool initiallyVisible)
    {
        _viewModel = viewModel;
        _userWantsVisible = initiallyVisible;

        InitializeComponent();
        DataContext = _viewModel;

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            PreventActivation(_hwnd);
            Reposition();
        };
        SizeChanged += (_, _) => Reposition();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _timer.Tick += (_, _) => Reposition();

        Loaded += (_, _) =>
        {
            _ready = true;
            Reposition();
            _timer.Start();
        };
    }

    /// <summary>Shows or hides the overlay (the tray/menu toggle). The taskbar itself may still hide it.</summary>
    public void SetOverlayVisible(bool visible)
    {
        _userWantsVisible = visible;
        Reposition();
    }

    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenMenu();
    }

    private void Overlay_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenMenu();
    }

    private void OpenMenu()
    {
        // Show after the current mouse message is fully processed; showing a WinForms
        // menu synchronously from inside a WPF MouseUp handler makes it close instantly.
        Dispatcher.BeginInvoke(
            new Action(() => MenuRequested?.Invoke(this, EventArgs.Empty)),
            DispatcherPriority.Background);
    }

    // ---------------------------------------------------------------- placement

    private void Reposition()
    {
        if (_repositioning || _hwnd == IntPtr.Zero) return;
        _repositioning = true;
        try
        {
            bool taskbarFound = TaskbarLocator.TryGetTaskbarRect(out IntPtr taskbar, out RectPx taskbarRect);
            bool taskbarUsable = taskbarFound
                && NativeMethods.TryIsWindowVisible(taskbar)
                && IntersectsWithScreen(taskbarRect);

            if (_ready)
            {
                // A missing shell window (Explorer restart, Windows 11 docking failure) keeps the
                // widget alive as a floating widget. A taskbar that exists but is auto-hidden
                // still hides the overlay with it, as before.
                ApplyVisible(_userWantsVisible && (taskbarUsable || !taskbarFound));
            }

            if (!IsVisible) return;

            double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1d;

            if (!taskbarUsable)
            {
                PositionFloating(scale);
                return;
            }

            RectPx screen = PrimaryScreenPx();

            bool horizontal = taskbarRect.Bottom >= screen.Bottom - 1 || taskbarRect.Top <= screen.Top + 1;
            bool flushRight = taskbarRect.Right >= screen.Right - 1;

            int x;
            int y;

            if (horizontal)
            {
                double heightDip = taskbarRect.Height / scale;
                if (Math.Abs(Height - heightDip) > 0.5) Height = heightDip;

                int widthPx = Math.Max(1, (int)Math.Round(ActualWidth * scale));
                RectPx tray = TaskbarLocator.TrayRect(taskbar, taskbarRect);

                x = tray.Left - widthPx - GapToTrayPx;
                if (x + widthPx > tray.Left) x = tray.Left - widthPx;
                x = Math.Max(x, taskbarRect.Left + EdgePaddingPx);
                y = taskbarRect.Top;
            }
            else
            {
                // Vertical taskbar (left or right edge): sit at its lower end.
                int widthPx = Math.Max(1, (int)Math.Round(ActualWidth * scale));
                int heightPx = Math.Max(1, (int)Math.Round(Height * scale));
                x = flushRight ? taskbarRect.Left + EdgePaddingPx : taskbarRect.Right - widthPx - EdgePaddingPx;
                x = Math.Max(x, taskbarRect.Left + EdgePaddingPx);
                y = taskbarRect.Bottom - heightPx - GapToTrayPx;
                if (y < taskbarRect.Top + EdgePaddingPx) y = taskbarRect.Top + EdgePaddingPx;
            }

            NativeMethods.TrySetWindowPos(
                _hwnd,
                IntPtr.Zero,
                x,
                y,
                0,
                0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

            // The taskbar lifts itself above other topmost windows whenever it is clicked,
            // which would bury the readout - so re-assert our z-order on every tick.
            NativeMethods.TrySetWindowPos(
                _hwnd,
                NativeMethods.HwndTopmost,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

            UpdateTextTheme(x, Math.Min(x + (int)Math.Round(ActualWidth * scale), taskbarRect.Right - 2), taskbarRect);
        }
        finally
        {
            _repositioning = false;
        }
    }

    /// <summary>
    /// Windows 11 fallback: dock to the bottom-right of the work area when the shell taskbar
    /// cannot be found. Keeps the monitor usable instead of leaving a stray window or hiding it.
    /// </summary>
    private void PositionFloating(double scale)
    {
        if (Math.Abs(Height - FallbackHeightDip) > 0.5) Height = FallbackHeightDip;

        RectPx work = PrimaryWorkAreaPx();
        int widthPx = Math.Max(1, (int)Math.Round(ActualWidth * scale));
        int heightPx = Math.Max(1, (int)Math.Round(Height * scale));
        int margin = Math.Max(1, (int)Math.Round(FallbackMarginPx * scale));

        int x = Math.Max(work.Left + EdgePaddingPx, work.Right - widthPx - margin);
        int y = Math.Max(work.Top + EdgePaddingPx, work.Bottom - heightPx - margin);

        NativeMethods.TrySetWindowPos(
            _hwnd,
            IntPtr.Zero,
            x,
            y,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        NativeMethods.TrySetWindowPos(
            _hwnd,
            NativeMethods.HwndTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private void ApplyVisible(bool visible)
    {
        if (visible == IsVisible) return;
        if (visible) Show();
        else Hide();
    }

    private static bool IntersectsWithScreen(RectPx rect) => rect.IntersectsWith(PrimaryScreenPx());

    private static RectPx PrimaryScreenPx()
    {
        try
        {
            System.Drawing.Rectangle bounds =
                System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            return new RectPx(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        }
        catch
        {
            // Display mode change / session teardown: assume a safe virtual screen.
            return new RectPx(0, 0, 1920, 1080);
        }
    }

    private static RectPx PrimaryWorkAreaPx()
    {
        try
        {
            System.Drawing.Rectangle bounds =
                System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                ?? new System.Drawing.Rectangle(0, 0, 1920, 1040);
            return new RectPx(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        }
        catch
        {
            return new RectPx(0, 0, 1920, 1040);
        }
    }

    // ---------------------------------------------------------------- contrast

    /// <summary>
    /// Picks black or white text by sampling the taskbar background, so the readout stays
    /// legible on dark (Windows 11 / dark theme) and light (light theme) taskbars alike.
    /// </summary>
    private void UpdateTextTheme(int leftPx, int rightPx, RectPx taskbarRect)
    {
        if (DateTime.UtcNow - _lastThemeSample < ThemeSampleInterval) return;

        int midY = taskbarRect.Top + taskbarRect.Height / 2;
        int probeX = rightPx + 4 <= taskbarRect.Right - 2 ? rightPx + 4 : leftPx - 4;
        if (probeX < taskbarRect.Left + 2 || probeX > taskbarRect.Right - 2) return;
        if (midY < taskbarRect.Top || midY > taskbarRect.Bottom) return;

        try
        {
            using var bitmap = new System.Drawing.Bitmap(1, 1);
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(probeX, midY, 0, 0, new System.Drawing.Size(1, 1));
            }

            System.Drawing.Color color = bitmap.GetPixel(0, 0);
            double luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
            bool darkText = luminance > 140;

            _lastThemeSample = DateTime.UtcNow;
            if (darkText == _darkTextOnLightBar) return;
            _darkTextOnLightBar = darkText;

            ValueText.Foreground = darkText ? DarkText : LightText;
            ValueText.Effect = darkText
                ? null
                : new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 4,
                    ShadowDepth = 0,
                    Opacity = 0.55,
                };
        }
        catch
        {
            // Screen sampling can fail during display mode changes; keep the previous colour.
        }
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// WS_EX_NOACTIVATE: clicking the readout must never steal focus from the application
    /// the user is working in (WPF's ShowActivated only covers the initial Show).
    /// </summary>
    private static void PreventActivation(IntPtr hwnd)
    {
        const int GwlExStyle = -20;
        const int WsExNoActivate = 0x08000000;

        try
        {
            int style = NativeMethods.TryGetWindowLong(hwnd, GwlExStyle);
            NativeMethods.TrySetWindowLong(hwnd, GwlExStyle, style | WsExNoActivate);
        }
        catch
        {
            // Style tweaks are best-effort; the window still works without them.
        }
    }
}
