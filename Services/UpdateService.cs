using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace SpeedMonitor.Services;

/// <summary>Outcome of staging a downloaded update next to the running exe.</summary>
public sealed record UpdateDownloadResult(bool Ok, string? Path = null, string? Error = null);

/// <summary>
/// Talks to GitHub: finds the newest release, downloads the exe and verifies its SHA-256
/// against the release's own SHA256SUMS.txt. Everything is best effort - a failure here
/// must never disturb the running monitor.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;

    public UpdateService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("ISM-Internet-Speed-Monitor");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }
    }

    /// <summary>Newer release, or null when up to date / offline / rate limited.</summary>
    public async Task<UpdateInfo?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CheckTimeout);

            string json = await _http.GetStringAsync(UpdateChecker.LatestReleaseUrl, timeout.Token)
                .ConfigureAwait(false);

            return UpdateChecker.ParseLatestRelease(json, currentVersion);
        }
        catch
        {
            // Offline, DNS failure, GitHub rate limit, HTML error page: just stay put.
            return null;
        }
    }

    /// <summary>
    /// Downloads <paramref name="update"/> to <paramref name="destination"/> and checks it
    /// against the release checksums. The file only appears at the destination once verified.
    /// </summary>
    public async Task<UpdateDownloadResult> DownloadAsync(
        UpdateInfo update, string destination, CancellationToken cancellationToken = default)
    {
        string partial = destination + ".part";

        try
        {
            string? directory = Path.GetDirectoryName(destination);
            if (directory is not null) Directory.CreateDirectory(directory);

            using (HttpResponseMessage response = await _http
                .GetAsync(update.ExeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                await using Stream source = await response.Content
                    .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var target = new FileStream(
                    partial, FileMode.Create, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            if (update.ChecksumsUrl is not null)
            {
                string? expected = await TryGetExpectedHashAsync(update, cancellationToken).ConfigureAwait(false);
                if (expected is null)
                {
                    TryDelete(partial);
                    return new UpdateDownloadResult(false, Error: "The release checksum file has no hash for the exe.");
                }

                string actual = await ComputeHashAsync(partial, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(partial);
                    return new UpdateDownloadResult(false, Error: "The downloaded update failed its SHA-256 check.");
                }
            }

            File.Move(partial, destination, overwrite: true);
            return new UpdateDownloadResult(true, destination);
        }
        catch (Exception ex)
        {
            TryDelete(partial);
            return new UpdateDownloadResult(false, Error: ex.Message);
        }
    }

    private async Task<string?> TryGetExpectedHashAsync(UpdateInfo update, CancellationToken cancellationToken)
    {
        try
        {
            string sums = await _http.GetStringAsync(update.ChecksumsUrl!, cancellationToken).ConfigureAwait(false);
            return UpdateChecker.FindChecksum(sums, UpdateChecker.ExeAssetName);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* a leftover .part file is harmless */ }
    }

    public void Dispose() => _http.Dispose();
}
