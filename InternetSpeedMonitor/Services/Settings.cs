using System.IO;
using System.Text.Json;

namespace InternetSpeedMonitor.Services;

/// <summary>
/// Application settings with serialization support
/// </summary>
public class AppSettings
{
    // Position settings
    public double WindowLeft { get; set; } = -1; // -1 = default (auto position)
    public double WindowTop { get; set; } = -1;
    
    // Size settings
    public double WindowWidth { get; set; } = 280;
    public double WindowHeight { get; set; } = 80;
    public double FontSize { get; set; } = 16;
    
    // Display settings
    public int RefreshIntervalMs { get; set; } = 1000; // 1 second default
    public bool UseBitsPerSecond { get; set; } = true; // true = Mbps, false = MBps
    
    // Theme settings (0 = Dark, 1 = Light, 2 = System)
    public int ThemeMode { get; set; } = 0; 
    
    // Transparency (0.1 to 1.0)
    public double Opacity { get; set; } = 1.0;
    
    // Background opacity (0.1 to 1.0)
    public double BackgroundOpacity { get; set; } = 1.0;
    
    // Corner radius
    public double CornerRadius { get; set; } = 8;
    
    // Position lock
    public bool IsPositionLocked { get; set; } = true;
    
    // Always on top
    public bool AlwaysOnTop { get; set; } = true;
    
    // Start minimized
    public bool StartMinimized { get; set; } = false;
    
    // Start with Windows
    public bool StartWithWindows { get; set; } = false;
    
    // Hidden on startup (go to tray)
    public bool HideOnStartup { get; set; } = false;
    
    // Hide on close (minimize to tray instead of exit)
    public bool HideOnClose { get; set; } = true;
    
    private static string SettingsFilePath => 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "InternetSpeedMonitor", "settings.json");
    
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                System.Diagnostics.Debug.WriteLine($"Settings loaded from: {SettingsFilePath}");
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Settings file not found, using defaults. Path: {SettingsFilePath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            System.Windows.MessageBox.Show($"Error loading settings: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        return new AppSettings();
    }
    
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsFilePath, json);
            
            // Show success message
            System.Windows.MessageBox.Show($"Settings saved successfully!\nLocation: {SettingsFilePath}", 
                "Settings Saved", 
                System.Windows.MessageBoxButton.OK, 
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error saving settings: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
