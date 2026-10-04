using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using InternetSpeedMonitor.Services;
using H.NotifyIcon;
using WinColor = System.Windows.Media.Color;
using DrawingColor = System.Drawing.Color;
using DrawingSolidBrush = System.Drawing.SolidBrush;

namespace InternetSpeedMonitor;

/// <summary>
/// Main window - displays speed and manages system tray icon
/// </summary>
public partial class MainWindow : Window
{
    private readonly NetworkSpeedService _speedService;
    private readonly AppSettings _settings;
    private bool _isClosing;
    private bool _isDragging;
    private System.Windows.Point _dragStartPoint;
    
    // Theme colors
    private readonly SolidColorBrush _darkTextGreen = new(WinColor.FromRgb(74, 222, 128));
    private readonly SolidColorBrush _darkTextPink = new(WinColor.FromRgb(244, 114, 182));
    private readonly SolidColorBrush _lightTextGreen = new(WinColor.FromRgb(22, 163, 74));
    private readonly SolidColorBrush _lightTextPink = new(WinColor.FromRgb(219, 39, 119));
    private readonly SolidColorBrush _darkBorder = new(WinColor.FromRgb(74, 74, 106));
    private readonly SolidColorBrush _lightBorder = new(WinColor.FromRgb(200, 200, 200));
    
    public MainWindow()
    {
        InitializeComponent();
        
        _settings = AppSettings.Load();
        ApplySettings();
        
        _speedService = new NetworkSpeedService();
        _speedService.SetRefreshInterval(_settings.RefreshIntervalMs);
        _speedService.SpeedUpdated += OnSpeedUpdated;
        
        SetupTrayIcon();
        PositionWindow();
        
        // Always show window on startup (unless explicitly set to start minimized)
        if (!_settings.StartMinimized && !_settings.HideOnStartup)
        {
            Show();
            Activate();
        }
        
        _speedService.Start();
    }
    
    private void ApplySettings()
    {
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
        FontSize = _settings.FontSize;
        Opacity = _settings.Opacity;
        
        Topmost = _settings.AlwaysOnTop;
        ResizeMode = _settings.IsPositionLocked ? ResizeMode.NoResize : ResizeMode.CanResize;
        
        ApplyTheme(_settings.ThemeMode);
        UpdateBackgroundOpacity(_settings.BackgroundOpacity);
    }
    
    public void ApplyBehaviorSettings(bool isPositionLocked, bool startMinimized, bool hideOnClose)
    {
        ResizeMode = isPositionLocked ? ResizeMode.NoResize : ResizeMode.CanResize;
    }
    
    private void ApplyTheme(int themeMode)
    {
        int actualTheme = themeMode;
        
        if (themeMode == 2) // System
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                actualTheme = (value is int i && i == 0) ? 0 : 1;
            }
            catch { }
        }
        
        if (actualTheme == 0) // Dark
        {
            MainBorderElement.Background = (LinearGradientBrush)FindResource("DarkBackgroundBrush");
            MainBorderElement.BorderBrush = _darkBorder;
            DownloadSpeedText.Foreground = _darkTextGreen;
            UploadSpeedText.Foreground = _darkTextPink;
            DownloadArrowText.Foreground = _darkTextGreen;
            UploadArrowText.Foreground = _darkTextPink;
        }
        else // Light - Improved colors
        {
            // Light theme with better contrast
            var lightBg = new LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(255, 255, 255),
                System.Windows.Media.Color.FromRgb(240, 240, 245),
                new System.Windows.Point(0, 0),
                new System.Windows.Point(1, 1));
            
            MainBorderElement.Background = lightBg;
            MainBorderElement.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 190));
            
            // Dark green for download, dark pink for upload (better contrast on light)
            DownloadSpeedText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 0));
            UploadSpeedText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 0, 80));
            DownloadArrowText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 0));
            UploadArrowText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 0, 80));
        }
    }
    
    private void SetupTrayIcon()
    {
        TrayIcon.Icon = CreateDefaultIcon();
        UpdateTrayIcon(0, 0);
    }
    
    private System.Drawing.Icon CreateDefaultIcon()
    {
        // Load the user's custom icon from file
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ISM.ico");
        
        if (File.Exists(iconPath))
        {
            try
            {
                return new System.Drawing.Icon(iconPath);
            }
            catch
            {
                // Fall back to default icon if loading fails
            }
        }
        
        // Fallback: Create a default icon programmatically
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(DrawingColor.FromArgb(99, 102, 241)); // Purple background #6366F1
        
        // Draw download arrow (green)
        using var downBrush = new DrawingSolidBrush(DrawingColor.FromArgb(74, 222, 128));
        g.FillPolygon(downBrush, new System.Drawing.Point[] {
            new(6, 10), new(14, 10), new(10, 18), new(6, 18)
        });
        
        // Draw upload arrow (light purple)
        using var upBrush = new DrawingSolidBrush(DrawingColor.FromArgb(167, 139, 250));
        g.FillPolygon(upBrush, new System.Drawing.Point[] {
            new(18, 14), new(26, 14), new(22, 6), new(18, 14)
        });
        
        var hIcon = bitmap.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }
    
    private void UpdateTrayIcon(double downloadSpeed, double uploadSpeed)
    {
        string unit;
        double displayDownload, displayUpload;
        
        double downloadBytes = downloadSpeed;
        double uploadBytes = uploadSpeed;
        
        // Convert to bits if needed
        if (_settings.UseBitsPerSecond)
        {
            downloadBytes *= 8;
            uploadBytes *= 8;
        }
        
        if (downloadBytes >= 1024 * 1024 || uploadBytes >= 1024 * 1024)
        {
            unit = _settings.UseBitsPerSecond ? "Gb/s" : "MB/s";
            displayDownload = downloadBytes / (1024 * 1024);
            displayUpload = uploadBytes / (1024 * 1024);
        }
        else
        {
            unit = _settings.UseBitsPerSecond ? "Mb/s" : "KB/s";
            displayDownload = downloadBytes / 1024;
            displayUpload = uploadBytes / 1024;
        }
        
        var tooltip = $"Internet Speed Monitor\nby MeTariqul\n↓ {displayDownload:F1} {unit}  ↑ {displayUpload:F1} {unit}";
        TrayIcon.ToolTipText = tooltip;
    }
    
    private void OnSpeedUpdated(object? sender, SpeedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            string unit;
            double displayDownload, displayUpload;
            
            double downloadBytes = e.DownloadSpeed;
            double uploadBytes = e.UploadSpeed;
            
            // Convert to bits if needed
            if (_settings.UseBitsPerSecond)
            {
                downloadBytes *= 8; // bytes to bits
                uploadBytes *= 8;
            }
            
            if (downloadBytes >= 1024 * 1024 || uploadBytes >= 1024 * 1024)
            {
                unit = _settings.UseBitsPerSecond ? "Gb/s" : "MB/s";
                displayDownload = downloadBytes / (1024 * 1024);
                displayUpload = uploadBytes / (1024 * 1024);
            }
            else
            {
                unit = _settings.UseBitsPerSecond ? "Mb/s" : "KB/s";
                displayDownload = downloadBytes / 1024;
                displayUpload = uploadBytes / 1024;
            }
            
            DownloadSpeedText.Text = $"{displayDownload:F1} {unit}";
            UploadSpeedText.Text = $"{displayUpload:F1} {unit}";
            
            UpdateTrayIcon(e.DownloadSpeed, e.UploadSpeed);
        });
    }
    
    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        
        // Check if saved position is valid (on screen)
        bool isValidPosition = _settings.WindowLeft >= 0 && 
                               _settings.WindowTop >= 0 &&
                               _settings.WindowLeft < workArea.Right &&
                               _settings.WindowTop < workArea.Bottom;
        
        if (isValidPosition)
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
        }
        else
        {
            // Default: bottom-right corner
            Left = workArea.Right - Width - 10;
            Top = workArea.Bottom - Height - 10;
        }
    }
    
    private void SaveSettings()
    {
        _settings.Save();
    }
    
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.IsPositionLocked) return;
        
        if (e.ClickCount == 1)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            CaptureMouse();
        }
    }
    
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPos = e.GetPosition(this);
            Left += currentPos.X - _dragStartPoint.X;
            Top += currentPos.Y - _dragStartPoint.Y;
        }
        base.OnMouseMove(e);
    }
    
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            // Settings will be saved when user clicks Save in Settings
        }
        base.OnMouseLeftButtonUp(e);
    }
    
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        // Settings will be saved when user clicks Save in Settings
    }
    
    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        // Position saved on mouse up
    }
    
    // Simple menu handlers
    private void ShowHideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (IsVisible)
            Hide();
        else
        {
            Show();
            Activate();
        }
    }
    
    private void TrayIcon_TrayLeftMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        // Double-click tray icon to show window
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
            Activate();
        }
    }
    
    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(this);
        settingsWindow.ShowDialog();
    }
    
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _isClosing = true;
        _speedService.Stop();
        _speedService.Dispose();
        TrayIcon.Dispose();
        Application.Current.Shutdown();
    }
    
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosing && _settings.HideOnClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
    
    protected override void OnClosed(EventArgs e)
    {
        _speedService.Dispose();
        TrayIcon.Dispose();
        base.OnClosed(e);
    }
    
    // Public methods for Settings window
    public void ApplyThemePublic(int themeMode)
    {
        ApplyTheme(themeMode);
    }
    
    public void UpdateCornerRadius(double radius)
    {
        MainBorderElement.CornerRadius = new CornerRadius(radius);
    }
    
    public void UpdateBackgroundOpacity(double opacity)
    {
        _settings.BackgroundOpacity = opacity;
        // Update background brush alpha
        var darkBrush = FindResource("DarkBackgroundBrush") as LinearGradientBrush;
        var lightBrush = FindResource("LightBackgroundBrush") as LinearGradientBrush;
        
        if (darkBrush != null)
        {
            foreach (var stop in darkBrush.GradientStops)
            {
                var color = stop.Color;
                color.A = (byte)(opacity * 255);
                stop.Color = color;
            }
        }
        
        if (lightBrush != null)
        {
            foreach (var stop in lightBrush.GradientStops)
            {
                var color = stop.Color;
                color.A = (byte)(opacity * 255);
                stop.Color = color;
            }
        }
    }
    
    public void UpdateRefreshRate(int milliseconds)
    {
        _settings.RefreshIntervalMs = milliseconds;
        _speedService.SetRefreshInterval(milliseconds);
    }
    
    public void UpdateUnit(bool useBitsPerSecond)
    {
        _settings.UseBitsPerSecond = useBitsPerSecond;
    }
    
    public void ReloadSettings()
    {
        var newSettings = AppSettings.Load();
        
        _settings.WindowLeft = newSettings.WindowLeft;
        _settings.WindowTop = newSettings.WindowTop;
        _settings.WindowWidth = newSettings.WindowWidth;
        _settings.WindowHeight = newSettings.WindowHeight;
        _settings.FontSize = newSettings.FontSize;
        _settings.RefreshIntervalMs = newSettings.RefreshIntervalMs;
        _settings.ThemeMode = newSettings.ThemeMode;
        _settings.Opacity = newSettings.Opacity;
        _settings.BackgroundOpacity = newSettings.BackgroundOpacity;
        _settings.CornerRadius = newSettings.CornerRadius;
        _settings.IsPositionLocked = newSettings.IsPositionLocked;
        _settings.AlwaysOnTop = newSettings.AlwaysOnTop;
        _settings.StartMinimized = newSettings.StartMinimized;
        _settings.StartWithWindows = newSettings.StartWithWindows;
        _settings.HideOnClose = newSettings.HideOnClose;
        _settings.UseBitsPerSecond = newSettings.UseBitsPerSecond;
        
        ApplySettings();
        _speedService.SetRefreshInterval(_settings.RefreshIntervalMs);
    }
}
