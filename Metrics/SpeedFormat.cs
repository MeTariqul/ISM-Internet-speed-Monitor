namespace SpeedMonitor.Metrics;

public enum UnitMode
{
    Auto,
    KiloBytes,
    Megabits,
}

public enum GraphView
{
    Net,
    Disk,
    System,
}

public static class SpeedFormat
{
    private const double K = 1024d;
    private const double M = K * 1024d;
    private const double G = M * 1024d;

    /// <summary>Human readable throughput, e.g. "1.24 MB/s", "9.8 Mbps", "—" when unavailable.</summary>
    public static string Format(double bytesPerSecond, UnitMode mode)
    {
        if (double.IsNaN(bytesPerSecond) || bytesPerSecond < 0) return "\u2014";

        if (mode == UnitMode.Megabits)
        {
            double mbps = bytesPerSecond * 8d / 1_000_000d;
            return mbps >= 100 ? $"{mbps:0} Mbps" : $"{mbps:0.0} Mbps";
        }

        if (bytesPerSecond >= G) return $"{bytesPerSecond / G:0.00} GB/s";
        if (bytesPerSecond >= M) return $"{bytesPerSecond / M:0.00} MB/s";
        if (mode == UnitMode.KiloBytes || bytesPerSecond >= K)
            return $"{bytesPerSecond / K:0.0} KB/s";
        return $"{bytesPerSecond:0} B/s";
    }

    /// <summary>Short throughput label used inside the graph, e.g. "1.2M", "340K", "9.8Mb".</summary>
    public static string Compact(double bytesPerSecond, UnitMode mode)
    {
        if (double.IsNaN(bytesPerSecond) || bytesPerSecond < 0) return "\u2014";

        if (mode == UnitMode.Megabits)
        {
            double mbps = bytesPerSecond * 8d / 1_000_000d;
            return mbps >= 100 ? $"{mbps:0}Mb" : $"{mbps:0.0}Mb";
        }

        if (bytesPerSecond >= G) return $"{bytesPerSecond / G:0.00}G";
        if (bytesPerSecond >= M) return $"{bytesPerSecond / M:0.0}M";
        if (bytesPerSecond >= K) return $"{bytesPerSecond / K:0}K";
        return $"{bytesPerSecond:0}B";
    }

    /// <summary>Round a raw maximum up to a pleasant axis maximum (1 / 2 / 5 × 10ⁿ).</summary>
    public static double NiceMax(double value)
    {
        if (double.IsNaN(value) || value <= 0) return 1d;
        double exponent = Math.Floor(Math.Log10(value));
        double fraction = value / Math.Pow(10, exponent);
        double nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * Math.Pow(10, exponent);
    }
}
