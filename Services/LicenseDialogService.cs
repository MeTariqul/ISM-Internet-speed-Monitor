using SpeedMonitor.Services;

namespace SpeedMonitor.Services;

/// <summary>
/// Central place that creates licensing dialogs. Windows are transient UI, so they are
/// newed per call instead of being registered in the container.
/// </summary>
public sealed class LicenseDialogService
{
    private readonly ILicenseService _license;

    public LicenseDialogService(ILicenseService license)
    {
        _license = license ?? throw new ArgumentNullException(nameof(license));
    }

    /// <summary>Startup gate. Returns true when the user activated successfully.</summary>
    public bool ShowActivation(string? message = null)
        => new LicenseWindow(_license, message).ShowDialog() == true;

    /// <summary>Opened from the tray/overlay menu to view or replace the key.</summary>
    public void ShowLicense() => new LicenseWindow(_license).ShowDialog();
}
