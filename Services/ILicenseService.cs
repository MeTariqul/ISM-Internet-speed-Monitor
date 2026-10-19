namespace SpeedMonitor.Services;

/// <summary>
/// Licensing surface consumed by the app. An interface (rather than the concrete
/// <see cref="LicenseService"/>) keeps the dialogs and startup gate testable without
/// touching the real %AppData% store or the network.
/// </summary>
public interface ILicenseService
{
    /// <summary>Stable per-machine id the license is bound to.</summary>
    string DeviceId { get; }

    /// <summary>The stored key, or null when this PC was never activated.</summary>
    string? LicenseKey { get; }

    /// <summary>Claims from the last successful <see cref="ValidateOffline"/>.</summary>
    LicenseClaims? Claims { get; }

    /// <summary>True when the signed token is due for its online re-check.</summary>
    bool NeedsRecheck { get; }

    LicenseResult ValidateOffline();

    Task<LicenseResult> ActivateAsync(string key);

    Task<LicenseResult> VerifyAsync();

    Task<KeyRequestResult> RequestKeyAsync(string email);
}
