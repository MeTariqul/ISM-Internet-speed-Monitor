using System.Text.Json;

namespace SpeedMonitor.Services;

/// <summary>Details of a newer release found on GitHub.</summary>
public sealed record UpdateInfo(Version Version, string Tag, string ExeUrl, string? ChecksumsUrl);

/// <summary>
/// Parsing and version rules for the self-updater. Kept away from the network so the
/// decision "is this release newer, and which asset do I take?" can be unit-tested.
/// </summary>
public static class UpdateChecker
{
    public const string ExeAssetName = "ISM-Internet-Speed-Monitor.exe";
    public const string ChecksumsAssetName = "SHA256SUMS.txt";

    public const string LatestReleaseUrl =
        "https://api.github.com/repos/MeTariqul/ISM-Internet-speed-Monitor/releases/latest";

    /// <summary>
    /// Parses the GitHub "latest release" payload. Returns <c>null</c> when the payload is
    /// unusable, the release has no exe asset, or it is not newer than <paramref name="currentVersion"/>.
    /// </summary>
    public static UpdateInfo? ParseLatestRelease(string? json, Version currentVersion)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out JsonElement tagElement)) return null;
            if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? tag = tagElement.GetString();
            Version? version = ParseTag(tag);
            if (version is null || !IsNewer(version, currentVersion)) return null;

            string? exeUrl = null;
            string? checksumsUrl = null;

            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out JsonElement nameElement)
                    || !asset.TryGetProperty("browser_download_url", out JsonElement urlElement))
                {
                    continue;
                }

                string? name = nameElement.GetString();
                string? url = urlElement.GetString();
                if (name is null || url is null) continue;

                if (string.Equals(name, ExeAssetName, StringComparison.OrdinalIgnoreCase)) exeUrl = url;
                else if (string.Equals(name, ChecksumsAssetName, StringComparison.OrdinalIgnoreCase)) checksumsUrl = url;
            }

            if (exeUrl is null) return null;

            return new UpdateInfo(version, tag!, exeUrl, checksumsUrl);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>"v1.5.0" or "1.5.0" parses; prerelease and malformed tags do not.</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        string text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];

        return Version.TryParse(text, out Version? version) ? version : null;
    }

    /// <summary>Compares ignoring missing components, so 1.5.0 and 1.5.0.0 are the same build.</summary>
    public static bool IsNewer(Version candidate, Version current) => Normalize(candidate) > Normalize(current);

    private static Version Normalize(Version version) => new(
        version.Major,
        version.Minor,
        Math.Max(version.Build, 0),
        Math.Max(version.Revision, 0));

    /// <summary>Picks a SHA-256 out of a "hash  file" list such as SHA256SUMS.txt.</summary>
    public static string? FindChecksum(string? checksums, string fileName)
    {
        if (string.IsNullOrWhiteSpace(checksums)) return null;

        foreach (string line in checksums.Split('\n'))
        {
            string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            string file = parts[^1].TrimStart('*');
            if (!string.Equals(file, fileName, StringComparison.OrdinalIgnoreCase)) continue;

            string hash = parts[0];
            if (hash.Length == 64 && hash.All(char.IsAsciiHexDigit)) return hash.ToLowerInvariant();
        }

        return null;
    }
}
