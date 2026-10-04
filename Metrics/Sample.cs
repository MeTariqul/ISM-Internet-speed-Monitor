namespace SpeedMonitor.Metrics;

/// <summary>One second worth of meter readings. NaN means "counter unavailable".</summary>
public readonly record struct Sample(
    DateTime Time,
    double Down,
    double Up,
    double DiskRead,
    double DiskWrite,
    double CpuPercent,
    double MemPercent,
    double MemUsedMb);
