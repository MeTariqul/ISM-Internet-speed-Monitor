using System.ComponentModel;
using System.Runtime.CompilerServices;
using SpeedMonitor.Metrics;
using SpeedMonitor.Services;

namespace SpeedMonitor.ViewModels;

/// <summary>Formats the current readings for the taskbar overlay and the tray tooltip.</summary>
public sealed class MonitorViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private Sample _last;
    private bool _hasSample;
    private UnitMode _units;

    public MonitorViewModel(AppSettings settings)
    {
        _settings = settings;
        _units = settings.UnitMode;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UnitMode Units
    {
        get => _units;
        set
        {
            if (_units == value) return;
            _units = value;
            _settings.Units = value switch
            {
                UnitMode.KiloBytes => "KB",
                UnitMode.Megabits => "Mbps",
                _ => "Auto",
            };
            Refresh();
        }
    }

    /// <summary>What the taskbar overlay shows, e.g. "↑ 1.24 MB/s   ↓ 340 KB/s".</summary>
    public string TaskbarText { get; private set; } = "\u2026";

    /// <summary>Tray icon tooltip (max 127 characters).</summary>
    public string TrayText { get; private set; } = "Speed Monitor";

    public void AddSample(Sample sample)
    {
        _last = sample;
        _hasSample = true;
        Refresh();
    }

    /// <summary>Recomputes the texts; call after a sample or after a display setting changed.</summary>
    public void Refresh()
    {
        Sample s = _last;

        string up = _hasSample ? SpeedFormat.Format(s.Up, Units) : "\u2014";
        string down = _hasSample ? SpeedFormat.Format(s.Down, Units) : "\u2014";
        string read = _hasSample ? SpeedFormat.Format(s.DiskRead, UnitMode.Auto) : "\u2014";
        string write = _hasSample ? SpeedFormat.Format(s.DiskWrite, UnitMode.Auto) : "\u2014";
        string cpu = _hasSample && !double.IsNaN(s.CpuPercent) ? $"{s.CpuPercent:0}%" : "\u2014";
        string ram = _hasSample && !double.IsNaN(s.MemPercent) ? $"{s.MemPercent:0}%" : "\u2014";

        var parts = new List<string>(6);
        switch (_settings.TaskbarFormat)
        {
            case "Disk":
                parts.Add($"R {read}");
                parts.Add($"W {write}");
                break;
            case "Both":
                parts.Add($"\u2191 {SpeedFormat.Compact(s.Up, Units)}");
                parts.Add($"\u2193 {SpeedFormat.Compact(s.Down, Units)}");
                parts.Add($"R {SpeedFormat.Compact(s.DiskRead, UnitMode.Auto)}");
                parts.Add($"W {SpeedFormat.Compact(s.DiskWrite, UnitMode.Auto)}");
                break;
            default:
                parts.Add($"\u2191 {up}");
                parts.Add($"\u2193 {down}");
                break;
        }

        if (_settings.ShowSystem)
        {
            parts.Add($"CPU {cpu}");
            parts.Add($"RAM {ram}");
        }

        TaskbarText = string.Join("   ", parts);
        TrayText = $"ISM {TaskbarText}";

        Raise(nameof(TaskbarText));
        Raise(nameof(TrayText));
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
