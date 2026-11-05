using System.Windows;
using System.Windows.Media;
using SpeedMonitor.Services;

namespace SpeedMonitor;

/// <summary>
/// Activation dialog shown at startup when the app is not licensed (cancelling exits the app),
/// and from the menu when the user wants to view or replace the current key.
/// </summary>
public partial class LicenseWindow : Window
{
    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E));
    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0x4E));

    private readonly ILicenseService _license;
    private bool _busy;
    private bool _closed;
    private bool _statusIsError;
    private bool _licensed;
    private string _activateLabel = "Activate";

    public LicenseWindow(ILicenseService license, string? message = null)
    {
        InitializeComponent();

        _license = license;
        KeyBox.Text = license.LicenseKey ?? string.Empty;

        LicenseResult offline = license.ValidateOffline();
        if (offline.Ok && offline.Claims is not null)
        {
            IntroText.Visibility = Visibility.Collapsed;
            KeyLabel.Visibility = Visibility.Collapsed;
            KeyBox.Visibility = Visibility.Collapsed;
            RequestSection.Visibility = Visibility.Collapsed;
            _licensed = true;
            _activateLabel = "Unregister";
            ActivateButton.Content = _activateLabel;
            QuitButton.Content = "Close";
            ShowThanks(true);
            SetStatus(Describe(offline.Claims), error: false);
        }
        else if (!string.IsNullOrWhiteSpace(message))
        {
            IntroText.Visibility = Visibility.Visible;
            KeyLabel.Visibility = Visibility.Visible;
            KeyBox.Visibility = Visibility.Visible;
            RequestSection.Visibility = Visibility.Visible;
            ShowThanks(false);
            SetStatus(message!, error: true);
        }
        else
        {
            IntroText.Visibility = Visibility.Visible;
            KeyLabel.Visibility = Visibility.Visible;
            KeyBox.Visibility = Visibility.Visible;
            RequestSection.Visibility = Visibility.Visible;
            ShowThanks(false);
        }

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(KeyBox.Text))
            {
                EmailBox.Focus();
            }
            else
            {
                KeyBox.Focus();
                KeyBox.SelectAll();
            }
        };

        Closed += (_, _) => _closed = true;
    }

    private async void Activate_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        if (_licensed)
        {
            _license.Unregister();
            _licensed = false;

            IntroText.Visibility = Visibility.Visible;
            KeyLabel.Visibility = Visibility.Visible;
            KeyBox.Visibility = Visibility.Visible;
            RequestSection.Visibility = Visibility.Visible;
            KeyBox.Text = string.Empty;
            _activateLabel = "Activate";
            ActivateButton.Content = _activateLabel;
            QuitButton.Content = "Quit";
            ShowThanks(false);
            SetStatus("License removed from this PC.", error: false);
            return;
        }

        string key = KeyBox.Text.Trim();
        if (key.Length == 0)
        {
            SetStatus("Enter a license key.", error: true);
            KeyBox.Focus();
            return;
        }

        SetBusy(true, "Activating...");
        try
        {
            if (await ActivateAsync(key) && !_closed)
            {
                DialogResult = true;
            }
        }
        catch (Exception ex)
        {
            if (!_closed) SetStatus("Activation failed: " + ex.Message, error: true);
        }
        finally
        {
            if (!_closed) SetBusy(false);
        }
    }

    private async void Request_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        SetBusy(true);
        SetStatus(string.Empty, error: false);
        SetRequestStatus("Requesting a key...", error: false);
        try
        {
            KeyRequestResult request = await _license.RequestKeyAsync(EmailBox.Text);
            if (!request.Ok)
            {
                if (!_closed) SetRequestStatus(request.Error ?? "Could not request a key.", error: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.Key))
            {
                if (!_closed)
                {
                    SetRequestStatus("Check your inbox - your key is on its way. Paste it above when it arrives.",
                        error: false);
                }
                return;
            }

            // Auto mode hands the key back immediately; activate it without another click.
            if (_closed) return;
            KeyBox.Text = request.Key;
            SetRequestStatus("Key received. Activating...", error: false);

            if (await ActivateAsync(request.Key) && !_closed)
            {
                DialogResult = true;
            }
        }
        catch (Exception ex)
        {
            if (!_closed) SetStatus("Could not request a key: " + ex.Message, error: true);
        }
        finally
        {
            if (!_closed) SetBusy(false);
        }
    }

    private async Task<bool> ActivateAsync(string key)
    {
        LicenseResult result = await _license.ActivateAsync(key);
        if (result.Ok) return true;

        if (!_closed) SetStatus(result.Error ?? "Activation failed.", error: true);
        return false;
    }

    private void KeyBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_statusIsError) SetStatus(string.Empty, error: false);
    }

    /// <summary>Key-request feedback lives next to the email box, not under the key field.</summary>
    private void SetRequestStatus(string text, bool error)
    {
        RequestStatusText.Text = text;
        RequestStatusText.Foreground = error ? ErrorBrush : InfoBrush;
        RequestStatusText.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void EmailBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;

        e.Handled = true;
        Request_Click(sender, e);
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        KeyBox.IsEnabled = !busy;
        EmailBox.IsEnabled = !busy;
        ActivateButton.IsEnabled = !busy;
        RequestButton.IsEnabled = !busy;
        QuitButton.IsEnabled = !busy;
        ActivateButton.Content = busy ? "Please wait..." : _activateLabel;

        if (status is not null) SetStatus(status, error: false);
    }

    private void SetStatus(string text, bool error)
    {
        _statusIsError = error && text.Length > 0;
        StatusText.Text = text;
        StatusText.Foreground = error ? ErrorBrush : InfoBrush;
        StatusText.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowThanks(bool show)
    {
        ThanksText.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            ThanksText.Text = "Thank you for registering ISM Internet Speed Monitor! Your license is active on this PC. Everything works offline; the license only checks in with the server when its re-check time arrives.";
        }
    }

    private static string Describe(LicenseClaims claims)
    {
        if (claims.ExpiresAt == 0) return "Licensed on this PC. This license does not expire.";

        DateTimeOffset expires = DateTimeOffset
            .FromUnixTimeMilliseconds(claims.ExpiresAt)
            .ToLocalTime();
        return $"Licensed on this PC until {expires:dd MMM yyyy, HH:mm}.";
    }
}
