using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace SpeedMonitor.Services;

/// <summary>Claims carried by a license token. Times are Unix milliseconds; 0 expiry means never.</summary>
public sealed record LicenseClaims(string DeviceId, long ExpiresAt, long RecheckAt);

/// <summary>Outcome of an activation, verification or offline token check.</summary>
/// <param name="NetworkFailure">
/// True when the server could not be reached. Offline-valid tokens stay usable in that case;
/// a definitive rejection (suspended, expired, revoked...) never sets it.
/// </param>
public sealed record LicenseResult(
    bool Ok,
    string? Error = null,
    string? Status = null,
    bool NetworkFailure = false,
    LicenseClaims? Claims = null)
{
    public static LicenseResult Success(LicenseClaims? claims = null) => new(true, Claims: claims);

    public static LicenseResult Fail(string error, string? status = null, bool networkFailure = false) =>
        new(false, error, status, networkFailure);
}

/// <summary>Outcome of an in-app "email me a key" request.</summary>
public sealed record KeyRequestResult(bool Ok, string? Error = null, string? Key = null);

/// <summary>
/// License client for the metariqul licensing service.
///
/// Activation is a one-time online call; afterwards the Ed25519-signed token is verified
/// offline at every launch. Only when the token's <c>r</c> (re-check) time has passed does the
/// app call the server again - that is where revocation, suspension and expiry land.
///
/// The token is stored in %AppData%\SpeedMonitor\license.json together with the user's key
/// (needed for the periodic re-check). Both are deleted on uninstall.
/// </summary>
public sealed class LicenseService
{
    private const string BaseUrl = "https://metariqul.vercel.app";
    private const string AppKey = "appk_EBFlOccJ5FiH2HbKaAkFRuZ7wJMgvTBR";
    private const string Platform = "win32";

    /// <summary>Offline verification key from the licensing dashboard. It can verify but not create tokens.</summary>
    private const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MCowBQYDK2VwAyEAR0bqZuQw+019bOdFLy/d73sSs1k7I6s30vXOFNN9F68=
        -----END PUBLIC KEY-----
        """;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly Ed25519PublicKeyParameters VerifyKey = LoadVerifyKey();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private string? _key;
    private string? _token;
    private LicenseClaims? _claims;

    public LicenseService()
    {
        DeviceId = ResolveDeviceId();
        Load();
    }

    /// <summary>Stable per-machine id the license is bound to (Windows MachineGuid).</summary>
    public string DeviceId { get; }

    public string? LicenseKey => _key;

    public LicenseClaims? Claims => _claims;

    /// <summary>True when the token is due for its online re-check.</summary>
    public bool NeedsRecheck =>
        _claims is not null &&
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= _claims.RecheckAt;

    /// <summary>
    /// Verifies the stored token without any network access: signature, device binding and expiry.
    /// Updates <see cref="Claims"/> on success.
    /// </summary>
    public LicenseResult ValidateOffline()
    {
        _claims = null;

        if (string.IsNullOrWhiteSpace(_token))
        {
            return LicenseResult.Fail("No license is activated on this PC.", "missing");
        }

        try
        {
            string[] parts = _token.Split('.');
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                return LicenseResult.Fail("The stored license is damaged. Activate it again.", "invalid_token");
            }

            // The signature covers the base64url payload text itself (not the decoded JSON).
            byte[] message = Encoding.ASCII.GetBytes(parts[0]);
            byte[] signature = Base64UrlDecode(parts[1]);
            if (!VerifySignature(message, signature))
            {
                return LicenseResult.Fail("The stored license failed its signature check.", "invalid_token");
            }

            byte[] payload = Base64UrlDecode(parts[0]);
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;

            string? device = root.TryGetProperty("d", out JsonElement deviceElement)
                ? deviceElement.GetString()
                : null;
            long expiresAt = ReadLong(root, "e");
            long recheckAt = ReadLong(root, "r");

            if (!string.Equals(device, DeviceId, StringComparison.Ordinal))
            {
                return LicenseResult.Fail("This license was activated on a different PC.", "wrong_device");
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (expiresAt > 0 && expiresAt <= now)
            {
                return LicenseResult.Fail("This license has expired. Activate a new key to continue.", "expired");
            }

            _claims = new LicenseClaims(device!, expiresAt, recheckAt);
            return LicenseResult.Success(_claims);
        }
        catch (Exception)
        {
            // A tampered or truncated token must never crash the app.
            return LicenseResult.Fail("The stored license is damaged. Activate it again.", "invalid_token");
        }
    }

    /// <summary>Activates <paramref name="key"/> for this machine and stores the returned token.</summary>
    public async Task<LicenseResult> ActivateAsync(string key)
    {
        key = key.Trim();
        if (key.Length == 0) return LicenseResult.Fail("Enter a license key.");

        ApiResponse response = await PostAsync(
            "/api/license/activate",
            new { key, deviceId = DeviceId, platform = Platform }).ConfigureAwait(false);

        if (!response.Reached)
        {
            return LicenseResult.Fail(response.Error!, networkFailure: true);
        }
        if (!response.Ok)
        {
            return LicenseResult.Fail(Friendly(response), response.Status);
        }
        if (string.IsNullOrWhiteSpace(response.Token))
        {
            return LicenseResult.Fail("The server accepted the key but returned no license token.");
        }

        string? previousKey = _key;
        string? previousToken = _token;

        _key = key;
        _token = response.Token;
        LicenseResult check = ValidateOffline();
        if (!check.Ok)
        {
            // Never keep a token this build cannot verify (clock skew, wrong device, ...).
            _key = previousKey;
            _token = previousToken;
            _claims = null;
            return check;
        }

        Save();
        return LicenseResult.Success();
    }

    /// <summary>Online re-check. Called when the token's re-check time has passed.</summary>
    public async Task<LicenseResult> VerifyAsync()
    {
        if (string.IsNullOrWhiteSpace(_key))
        {
            return LicenseResult.Fail("No license key is stored on this PC.", "missing");
        }

        ApiResponse response = await PostAsync(
            "/api/license/verify",
            new { key = _key, deviceId = DeviceId }).ConfigureAwait(false);

        if (!response.Reached)
        {
            return LicenseResult.Fail(response.Error!, networkFailure: true);
        }

        if (!response.Ok)
        {
            // Suspended / expired / revoked / device removed: drop the token so the next launch locks.
            _token = null;
            _claims = null;
            Save();
            return LicenseResult.Fail(Friendly(response), response.Status);
        }

        if (!string.IsNullOrWhiteSpace(response.Token))
        {
            _token = response.Token;
            Save();
        }

        return ValidateOffline();
    }

    /// <summary>
    /// Asks the service to email a single-device key. The dashboard is set to Auto, so the
    /// response may already carry the key; when it does not, the user is told to check their inbox.
    /// </summary>
    public async Task<KeyRequestResult> RequestKeyAsync(string email)
    {
        email = email.Trim();
        if (!LooksLikeEmail(email))
        {
            return new KeyRequestResult(false, "Enter a valid email address.");
        }

        ApiResponse response = await PostAsync("/api/license/request", new { email }).ConfigureAwait(false);

        if (!response.Reached)
        {
            return new KeyRequestResult(false, response.Error);
        }
        if (!response.Ok)
        {
            return new KeyRequestResult(false, response.Error ?? Friendly(response));
        }

        return new KeyRequestResult(true, Key: response.Key);
    }

    internal static string StorePath => Path.Combine(DirectoryPath, "license.json");

    internal static string DeviceIdPath => Path.Combine(DirectoryPath, "device.id");

    internal static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpeedMonitor");

    // ------------------------------------------------------------------ storage

    private void Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return;

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(StorePath));
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("key", out JsonElement key)) _key = key.GetString();
            if (root.TryGetProperty("token", out JsonElement token)) _token = token.GetString();
        }
        catch
        {
            // A corrupt store simply behaves like "not activated"; the user can activate again.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(new { key = _key, token = _token }, JsonOptions));
        }
        catch
        {
            // Licensing still works for this session; the next launch will ask again.
        }
    }

    // ------------------------------------------------------------------ device id

    private static string ResolveDeviceId()
    {
        // Windows' canonical, stable per-machine id (what node-machine-id reads too).
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography", writable: false);
            if (key?.GetValue("MachineGuid") is string guid && Guid.TryParse(guid.Trim(), out Guid parsed))
            {
                return parsed.ToString("D");
            }
        }
        catch { /* fall back to a generated id */ }

        try
        {
            if (File.Exists(DeviceIdPath) && Guid.TryParse(File.ReadAllText(DeviceIdPath).Trim(), out Guid existing))
            {
                return existing.ToString("D");
            }

            Guid created = Guid.NewGuid();
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(DeviceIdPath, created.ToString("D"));
            return created.ToString("D");
        }
        catch
        {
            return Environment.MachineName.ToLowerInvariant();
        }
    }

    // ------------------------------------------------------------------ http

    private sealed record ApiResponse(
        bool Reached,
        bool Ok,
        string? Error = null,
        string? Status = null,
        string? Token = null,
        string? Key = null);

    private static async Task<ApiResponse> PostAsync(string path, object body)
    {
        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path) { Content = content };
            request.Headers.TryAddWithoutValidation("x-app-key", AppKey);

            using HttpResponseMessage response = await Http.SendAsync(request).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new ApiResponse(false, false,
                    $"The license server returned HTTP {(int)response.StatusCode}.");
            }

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            bool ok = root.TryGetProperty("ok", out JsonElement okElement) &&
                      okElement.ValueKind == JsonValueKind.True;
            string? status = GetString(root, "status") ?? GetString(root, "code");
            string? error = GetString(root, "error") ?? GetString(root, "message");
            if (error is null && !ok)
            {
                error = $"The license server returned HTTP {(int)response.StatusCode}.";
            }

            return new ApiResponse(
                Reached: true,
                Ok: ok,
                Error: error,
                Status: status,
                Token: GetString(root, "token"),
                Key: GetString(root, "key") ?? GetString(root, "licenseKey"));
        }
        catch (JsonException)
        {
            return new ApiResponse(false, false,
                "The license server returned an unexpected response. Try again.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ApiResponse(false, false,
                "Could not reach the license server. Check your internet connection and try again.");
        }
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static long ReadLong(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement element)) return 0;

        return element.ValueKind switch
        {
            JsonValueKind.Number => ReadNumber(element),
            JsonValueKind.String when long.TryParse(
                element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) => value,
            _ => 0,
        };
    }

    private static long ReadNumber(JsonElement element)
    {
        if (element.TryGetInt64(out long integer)) return integer;
        // Some serializers emit whole milliseconds as 1.7e12 / 1700000000000.0.
        return element.TryGetDouble(out double number) ? (long)number : 0;
    }

    private static string Friendly(ApiResponse response)
    {
        string code = (response.Status ?? response.Error ?? string.Empty).Trim().ToLowerInvariant();
        return code switch
        {
            "expired" => "This license key has expired.",
            "suspended" => "This license key has been suspended.",
            "device_limit" => "This key is already active on the maximum number of devices.",
            "revoked_device" => "This device has been removed from the license.",
            "revoked" => "This license has been revoked.",
            "invalid" or "not_found" => "That license key is not valid.",
            _ => response.Error ?? "The license server rejected the request.",
        };
    }

    private static bool LooksLikeEmail(string email)
    {
        int at = email.IndexOf('@');
        int dot = email.LastIndexOf('.');
        return at > 0 && dot > at + 1 && dot < email.Length - 1 && !email.Contains(' ');
    }

    // ------------------------------------------------------------------ crypto

    private static Ed25519PublicKeyParameters LoadVerifyKey()
    {
        string pem = PublicKeyPem
            .Replace("-----BEGIN PUBLIC KEY-----", string.Empty, StringComparison.Ordinal)
            .Replace("-----END PUBLIC KEY-----", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();

        return (Ed25519PublicKeyParameters)PublicKeyFactory.CreateKey(Convert.FromBase64String(pem));
    }

    private static bool VerifySignature(byte[] message, byte[] signature)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(false, VerifyKey);
        verifier.BlockUpdate(message, 0, message.Length);
        return verifier.VerifySignature(signature);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };
        return Convert.FromBase64String(padded);
    }
}
