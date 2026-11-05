using System.Net.NetworkInformation;
using System.Windows.Threading;
using SpeedMonitor.Services;

/// <summary>
/// Periodically re-checks the stored license with the server (every minute by default) while the
/// app runs. A definite revocation (suspended / expired / revoked / device removed) raises
/// <see cref="Revoked"/> so the UI can show the activation dialog again; network failures leave
/// the offline token untouched.
/// </summary>
public sealed class LicenseWatcher : IDisposable
{
    private readonly ILicenseService _license;
    private readonly LicenseDialogService _dialogs;
    private readonly Dispatcher _dispatcher;
    private readonly TimeSpan _interval;
    private readonly Timer _timer;

    private volatile bool _disposed;
    private bool _checking;

    /// <summary>
    /// Raises <see cref="Revoked"/> on the UI thread when the license is definitely revoked.
    /// </summary>
    public event EventHandler<LicenseResult>? Revoked;

    public LicenseWatcher(ILicenseService license, LicenseDialogService dialogs, Dispatcher dispatcher, TimeSpan? interval = null)
    {
        _license = license;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _interval = interval ?? TimeSpan.FromMinutes(1);
        _timer = new Timer(_ => _ = CheckAsync(), null, _interval, _interval);
    }

    private async Task CheckAsync()
    {
        if (_disposed || !NetworkInterface.GetIsNetworkAvailable()) return;
        if (_checking) return;
        _checking = true;
        try
        {
            LicenseResult result = await Task.Run(() => _license.VerifyAsync()).ConfigureAwait(false);
            if (result.Ok) return;

            if (IsDefinitelyGone(result))
            {
                LicenseResult? snapshot = result;
                _ = _dispatcher.InvokeAsync(() => Revoked?.Invoke(this, snapshot));
            }
        }
        catch
        {
            // A failed check must never take the monitor down; the offline token governs meanwhile.
        }
        finally { _checking = false; }
    }

    private static bool IsDefinitelyGone(LicenseResult result)
    {
        if (result == null) return false;
        return result.Status is not null &&
            (result.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase) ||
             result.Status.Equals("expired", StringComparison.OrdinalIgnoreCase) ||
             result.Status.Equals("revoked", StringComparison.OrdinalIgnoreCase) ||
             result.Status.Equals("revoked_device", StringComparison.OrdinalIgnoreCase) ||
             result.Status.Equals("device_limit", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }
}
