namespace SpeedMonitor.Metrics;

/// <summary>
/// Pure, allocation-free conversions shared by the sampler and the formatters.
/// Kept separate from the performance counters so it can be unit tested without Windows.
/// </summary>
public static class BandwidthMath
{
    private const double BitsPerByte = 8d;
    private const double Megabit = 1_000_000d;
    private const double Mebibyte = 1024d * 1024d;

    /// <summary>Bytes/second to megabits/second (decimal megabits, as network vendors advertise).</summary>
    public static double ToMegabitsPerSecond(double bytesPerSecond)
        => IsValid(bytesPerSecond) ? bytesPerSecond * BitsPerByte / Megabit : double.NaN;

    /// <summary>Bytes to mebibytes (binary unit used by the B/KB/MB/GB formatter).</summary>
    public static double ToMebibytes(double bytes)
        => IsValid(bytes) ? bytes / Mebibyte : double.NaN;

    /// <summary>Used percentage clamped to 0..100, or NaN when total is unknown.</summary>
    public static double PercentUsed(double total, double available)
    {
        if (!IsValid(total) || total <= 0 || !IsValid(available)) return double.NaN;
        return Math.Clamp((total - available) / total * 100d, 0d, 100d);
    }

    /// <summary>Used amount in the same unit as the inputs, or NaN when unknown.</summary>
    public static double Used(double total, double available)
    {
        if (!IsValid(total) || !IsValid(available)) return double.NaN;
        return Math.Max(0d, total - available);
    }

    /// <summary>NaN means "counter unavailable" everywhere in this app; negatives are invalid too.</summary>
    public static bool IsValid(double value) => !double.IsNaN(value) && value >= 0d;
}
