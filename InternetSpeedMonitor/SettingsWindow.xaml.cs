using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using InternetSpeedMonitor.Services;

namespace InternetSpeedMonitor;

/// <summary>
/// Settings window for configuring all application options
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly MainWindow? _mainWindow;
    private bool _isLoading = true;
    
    public SettingsWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        _mainWindow = Application.Current.MainWindow as MainWindow;
        LoadCurrentSettings();
        _isLoading = false;
    }
    
    public SettingsWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _settings = AppSettings.Load();
        LoadCurrentSettings();
        _isLoading = false;
    }
    
    private void LoadCurrentSettings()
    {
        // Transparency
        TransparencySlider.Value = _settings.Opacity;
        BackgroundOpacitySlider.Value = _settings.BackgroundOpacity;
        
        // Corner radius
        CornerRadiusSlider.Value = _settings.CornerRadius;
        
        // Unit
        UnitComboBox.SelectedIndex = _settings.UseBitsPerSecond ? 0 : 1;
        
        // Refresh rate
        RefreshComboBox.SelectedIndex = _settings.RefreshIntervalMs switch
        {
            500 => 0,
            1000 => 1,
            2000 => 2,
            5000 => 3,
            _ => 1
        };
        
        // Size
        WidthSlider.Value = _settings.WindowWidth;
        HeightSlider.Value = _settings.WindowHeight;
        
        // Behavior
        AlwaysOnTopCheckBox.IsChecked = _settings.AlwaysOnTop;
        LockPositionCheckBox.IsChecked = _settings.IsPositionLocked;
        StartMinimizedCheckBox.IsChecked = _settings.StartMinimized;
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        HideOnCloseCheckBox.IsChecked = _settings.HideOnClose;
    }
    
    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Theme is now fixed to Dark only
    }
    
    private void ApplyThemePreview(int themeMode)
    {
        if (themeMode == 0) // Dark
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 46));
            UpdateTextColors(true);
        }
        else if (themeMode == 1) // Light
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 250));
            UpdateTextColors(false);
        }
        else // System
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                bool isDark = value is int i && i == 0;
                Background = new System.Windows.Media.SolidColorBrush(
                    isDark ? System.Windows.Media.Color.FromRgb(30, 30, 46) : System.Windows.Media.Color.FromRgb(245, 245, 250));
                UpdateTextColors(!isDark);
            }
            catch { }
        }
    }
    
    private void UpdateTextColors(bool isLight)
    {
        // Update StackPanel children recursively
        UpdateVisualChildColors(Content as DependencyObject, isLight);
    }
    
    private void UpdateVisualChildColors(DependencyObject parent, bool isLight)
    {
        if (parent == null) return;
        
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            
            if (child is System.Windows.Controls.TextBlock tb)
            {
                // Skip section headers with purple color
                var colorStr = tb.Foreground?.ToString() ?? "";
                if (!colorStr.Contains("7C3AED"))
                {
                    tb.Foreground = new System.Windows.Media.SolidColorBrush(
                        isLight ? System.Windows.Media.Color.FromRgb(40, 40, 40) : System.Windows.Media.Color.FromRgb(224, 224, 224));
                }
            }
            else if (child is System.Windows.Controls.Border border)
            {
                if (isLight)
                    border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                else
                    border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 68));
            }
            
            UpdateVisualChildColors(child, isLight);
        }
    }
    
    private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        _settings.Opacity = TransparencySlider.Value;
        _mainWindow!.Opacity = _settings.Opacity;
    }
    
    private void BackgroundOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        _settings.BackgroundOpacity = BackgroundOpacitySlider.Value;
        _mainWindow?.UpdateBackgroundOpacity(_settings.BackgroundOpacity);
    }
    
    private void CornerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        _settings.CornerRadius = CornerRadiusSlider.Value;
        _mainWindow?.UpdateCornerRadius(_settings.CornerRadius);
    }
    
    private void UnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (UnitComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _settings.UseBitsPerSecond = bool.Parse(tag);
            _mainWindow?.UpdateUnit(_settings.UseBitsPerSecond);
        }
    }
    
    private void RefreshComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (RefreshComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _settings.RefreshIntervalMs = int.Parse(tag);
            _mainWindow?.UpdateRefreshRate(_settings.RefreshIntervalMs);
        }
    }
    
    private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        _settings.WindowWidth = WidthSlider.Value;
        _settings.WindowHeight = HeightSlider.Value;
        
        if (_mainWindow != null)
        {
            _mainWindow.Width = _settings.WindowWidth;
            _mainWindow.Height = _settings.WindowHeight;
        }
    }
    
    private void AlwaysOnTopCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settings.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true;
        if (_mainWindow != null)
        {
            _mainWindow.Topmost = _settings.AlwaysOnTop;
        }
    }
    
    private void LockPositionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settings.IsPositionLocked = LockPositionCheckBox.IsChecked == true;
        if (_mainWindow != null)
        {
            _mainWindow.ApplyBehaviorSettings(_settings.IsPositionLocked, _settings.StartMinimized, _settings.HideOnClose);
        }
    }
    
    private void StartMinimizedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settings.StartMinimized = StartMinimizedCheckBox.IsChecked == true;
    }
    
    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        SetStartWithWindows(_settings.StartWithWindows);
    }
    
    private void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            
            if (key != null)
            {
                if (enable)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("InternetSpeedMonitor", $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue("InternetSpeedMonitor", false);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting auto-start: {ex.Message}");
        }
    }
    
    private void HideOnCloseCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settings.HideOnClose = HideOnCloseCheckBox.IsChecked == true;
    }
    
    private void ResetPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WindowLeft = -1;
        _settings.WindowTop = -1;
        
        if (_mainWindow != null)
        {
            var workArea = SystemParameters.WorkArea;
            _mainWindow.Left = workArea.Right - _mainWindow.Width - 10;
            _mainWindow.Top = workArea.Bottom - _mainWindow.Height - 10;
        }
    }
    
    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        _settings.Save();
        
        // Apply settings to main window before closing
        if (_mainWindow != null)
        {
            _mainWindow.ReloadSettings();
            _mainWindow.UpdateUnit(_settings.UseBitsPerSecond);
        }
        
        DialogResult = true;
        Close();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Reload original settings
        var original = AppSettings.Load();
        
        _settings.WindowLeft = original.WindowLeft;
        _settings.WindowTop = original.WindowTop;
        _settings.WindowWidth = original.WindowWidth;
        _settings.WindowHeight = original.WindowHeight;
        _settings.FontSize = original.FontSize;
        _settings.RefreshIntervalMs = original.RefreshIntervalMs;
        _settings.UseBitsPerSecond = original.UseBitsPerSecond;
        _settings.ThemeMode = original.ThemeMode;
        _settings.Opacity = original.Opacity;
        _settings.BackgroundOpacity = original.BackgroundOpacity;
        _settings.CornerRadius = original.CornerRadius;
        _settings.IsPositionLocked = original.IsPositionLocked;
        _settings.AlwaysOnTop = original.AlwaysOnTop;
        _settings.StartMinimized = original.StartMinimized;
        _settings.StartWithWindows = original.StartWithWindows;
        _settings.HideOnClose = original.HideOnClose;
        
        // Restore main window
        if (_mainWindow != null)
        {
            _mainWindow.ReloadSettings();
            _mainWindow.UpdateUnit(original.UseBitsPerSecond);
        }
        
        // Restore auto-start setting
        SetStartWithWindows(original.StartWithWindows);
        
        DialogResult = false;
        Close();
    }
    
    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch { }
    }
    
    private void Slider_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is Slider slider)
        {
            if (e.Delta > 0)
            {
                slider.Value = Math.Min(slider.Value + slider.TickFrequency, slider.Maximum);
            }
            else
            {
                slider.Value = Math.Max(slider.Value - slider.TickFrequency, slider.Minimum);
            }
            e.Handled = true;
        }
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Cancel_Click(sender, e);
    }
    
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
