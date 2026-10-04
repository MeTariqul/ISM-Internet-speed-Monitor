using System.Diagnostics;

namespace SpeedMonitor.Metrics;

/// <summary>
/// Samples network, disk, CPU and memory once per second on a background thread
/// and raises <see cref="Sampled"/> with the result.
/// </summary>
public sealed class MetricsSampler : IDisposable
{
    private static readonly string[] VirtualAdapterMarkers =
    {
        "isatap", "teredo", "loopback", "vethernet", "hyper-v", "wsl", "bluetooth",
        "wi-fi direct", "kernel debug", "windows sandbox", "wan miniport",
        "vpn", "wireguard", "wintun", "tap-windows", "nordlynx",
    };

    private readonly object _sync = new();
    private readonly List<PerformanceCounter> _netIn = new();
    private readonly List<PerformanceCounter> _netOut = new();
    private readonly PerformanceCounter? _diskRead;
    private readonly PerformanceCounter? _diskWrite;
    private readonly PerformanceCounter? _cpu;
    private readonly PerformanceCounter? _memAvailable;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;

    public MetricsSampler(string? adapter)
    {
        _diskRead = Create("LogicalDisk", "Disk Read Bytes/sec", "_Total");
        _diskWrite = Create("LogicalDisk", "Disk Write Bytes/sec", "_Total");
        _cpu = Create("Processor", "% Processor Time", "_Total");
        _memAvailable = Create("Memory", "Available MBytes", null);

        lock (_sync)
        {
            BuildNetworkCore(adapter);
        }
    }

    /// <summary>Raised once per second with a fresh reading.</summary>
    public event Action<Sample>? Sampled;

    /// <summary>Every network interface visible to the performance counters.</summary>
    public static string[] GetAdapterInstances()
    {
        try
        {
            return new PerformanceCounterCategory("Network Interface")
                .GetInstanceNames()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Switch the network source: null = auto (sum of non-virtual adapters).</summary>
    public void SetAdapter(string? adapter)
    {
        if (_disposed) return;
        lock (_sync)
        {
            BuildNetworkCore(adapter);
        }
    }

    public void Start()
    {
        if (_loop is not null || _disposed) return;

        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        _loop = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    Sampled?.Invoke(TakeSample());
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
        });
    }

    private Sample TakeSample()
    {
        lock (_sync)
        {
            double down = Sum(_netIn);
            double up = Sum(_netOut);
            double read = Read(_diskRead);
            double write = Read(_diskWrite);
            double cpu = Read(_cpu);
            double available = Read(_memAvailable);

            double totalMb = SystemInfo.TotalPhysicalMb;
            double memPercent = double.IsNaN(available) || totalMb <= 0
                ? double.NaN
                : (totalMb - available) / totalMb * 100d;
            double usedMb = double.IsNaN(available) ? double.NaN : totalMb - available;

            return new Sample(DateTime.Now, down, up, read, write, cpu, memPercent, usedMb);
        }
    }

    private void BuildNetworkCore(string? adapter)
    {
        foreach (PerformanceCounter counter in _netIn) counter.Dispose();
        foreach (PerformanceCounter counter in _netOut) counter.Dispose();
        _netIn.Clear();
        _netOut.Clear();

        IEnumerable<string> instances;
        if (adapter is null)
        {
            // Auto: every interface except loopbacks / tunnels / virtual switches whose
            // traffic is already mirrored by a physical adapter (double counting otherwise).
            instances = GetAdapterInstances()
                .Where(name => !VirtualAdapterMarkers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase)));
        }
        else
        {
            instances = new[] { adapter };
        }

        foreach (string name in instances)
        {
            PerformanceCounter? rx = Create("Network Interface", "Bytes Received/sec", name);
            PerformanceCounter? tx = Create("Network Interface", "Bytes Sent/sec", name);
            if (rx is not null) _netIn.Add(rx);
            if (tx is not null) _netOut.Add(tx);
        }
    }

    private static PerformanceCounter? Create(string category, string counter, string? instance)
    {
        try
        {
            PerformanceCounter created = instance is null
                ? new PerformanceCounter(category, counter)
                : new PerformanceCounter(category, counter, instance);

            // Establishes the baseline the rate is computed against; the value is discarded.
            created.NextValue();
            return created;
        }
        catch
        {
            // Category missing, localized name mismatch, instance gone, insufficient rights, ...
            return null;
        }
    }

    private static double Read(PerformanceCounter? counter)
    {
        if (counter is null) return double.NaN;
        try
        {
            double value = counter.NextValue();
            return double.IsNaN(value) ? double.NaN : Math.Max(0, value);
        }
        catch
        {
            // Interface unplugged or counter instance removed; keep the meter alive.
            return double.NaN;
        }
    }

    private static double Sum(List<PerformanceCounter> counters)
    {
        double total = 0;
        bool any = false;
        foreach (PerformanceCounter counter in counters)
        {
            double value = Read(counter);
            if (!double.IsNaN(value))
            {
                total += value;
                any = true;
            }
        }
        return any ? total : double.NaN;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _cts?.Dispose();

        lock (_sync)
        {
            foreach (PerformanceCounter counter in _netIn) counter.Dispose();
            foreach (PerformanceCounter counter in _netOut) counter.Dispose();
            _netIn.Clear();
            _netOut.Clear();
            _diskRead?.Dispose();
            _diskWrite?.Dispose();
            _cpu?.Dispose();
            _memAvailable?.Dispose();
        }
    }
}
