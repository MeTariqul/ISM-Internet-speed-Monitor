using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpeedMonitor.Metrics;

namespace SpeedMonitor.Services;

/// <summary>Persisted user preferences (%AppData%\SpeedMonitor\settings.json).</summary>
public sealed class AppSettings
{
    public string Units { get; set; } = "Auto";
    public string? Adapter { get; set; }
    public bool StartWithWindows { get; set; }

    /// <summary>"Network" (default), "Disk" or "Both".</summary>
    public string TaskbarFormat { get; set; } = "Network";

    public bool ShowSystem { get; set; }
    public bool ShowTaskbarText { get; set; } = true;

    private const string AutostartValueName = "SpeedMonitor";

    /// <summary>Derived from <see cref="Units"/> - never persisted, never read from disk.</summary>
    [JsonIgnore]
    public UnitMode UnitMode => Units switch
    {
        "KB" => UnitMode.KiloBytes,
        "Mbps" => UnitMode.Megabits,
        _ => UnitMode.Auto,
    };

    /// <summary>Where the settings live; internal so <see cref="SelfInstall"/> can remove it on uninstall.</summary>
    internal static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpeedMonitor",
        "settings.json");

    /// <summary>Set while uninstalling so the final exit cannot resurrect the deleted settings file.</summary>
    internal static bool SuppressSave { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (loaded is not null) return loaded;
            }
        }
        catch { /* corrupt or unreadable settings simply fall back to defaults */ }

        return new AppSettings();
    }

    public void Save()
    {
        if (SuppressSave) return;
        try
        {
            string? dir = Path.GetDirectoryName(FilePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* never crash over settings */ }
    }

    public static bool IsAutostartEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
            return key?.GetValue(AutostartValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyAutostart(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null) return;

            if (enabled)
            {
                // Prefer the installed copy so uninstalling/keeping the download around cannot break autostart.
                string? path = SelfInstall.TargetPath;
                if (path is not null) key.SetValue(AutostartValueName, $"\"{path}\"");
            }
            else
            {
                key.DeleteValue(AutostartValueName, throwOnMissingValue: false);
            }
        }
        catch { /* registry may be locked down */ }
    }
}
