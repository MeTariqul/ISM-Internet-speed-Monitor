using System.Windows;
using System.Windows.Threading;
using SpeedMonitor.Metrics;
using SpeedMonitor.ViewModels;

namespace SpeedMonitor.Services;

/// <summary>
/// Application composition target: owns the runtime wiring (sampler -> view model -> tray)
/// and the exit sequence. <see cref="App"/> keeps only process-level concerns such as
/// command-line handling and the single-instance mutex.
/// </summary>
public sealed class AppController
{
    private readonly AppSettings _settings;
    private readonly MetricsSampler _sampler;
    private readonly MonitorViewModel _viewModel;
    private readonly TaskbarMenu _menu;
    private readonly TaskbarWindow _window;
    private readonly TrayIconService _tray;
    private readonly UpdateService _updates;
    private readonly Dispatcher _dispatcher;

    private bool _started;
    private bool _exiting;
    private int _updateRunning;

    public AppController(
        AppSettings settings,
        MetricsSampler sampler,
        MonitorViewModel viewModel,
        TaskbarMenu menu,
        TaskbarWindow window,
        TrayIconService tray,
        UpdateService updates,
        Dispatcher dispatcher)
    {
        _settings = settings;
        _sampler = sampler;
        _viewModel = viewModel;
        _menu = menu;
        _window = window;
        _tray = tray;
        _updates = updates;
        _dispatcher = dispatcher;
    }

    /// <summary>Starts sampling and shows the overlay. Must be called on the UI thread.</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;

        _sampler.Sampled += OnSampled;
        _sampler.Start();

        if (_settings.ShowTaskbarText) _window.Show();
        _tray.SetText(_viewModel.TrayText);

        if (_settings.AutoUpdate) _ = Task.Run(() => RunUpdateCheckAsync(automatic: true));
    }

    /// <summary>Opens the shared settings menu at the current cursor position.</summary>
    public void ShowMenu() => _menu.ShowAt(System.Windows.Forms.Cursor.Position);

    /// <summary>Menu entry: check now and ask before installing.</summary>
    public void CheckForUpdatesManually() => _ = Task.Run(() => RunUpdateCheckAsync(automatic: false));

    /// <summary>Raised once per second on the sampler's thread; marshals to the UI thread.</summary>
    private void OnSampled(Sample sample)
    {
        _dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.AddSample(sample);
            _tray.SetText(_viewModel.TrayText);
        }));
    }

    /// <summary>Saves settings, closes the overlay and shuts the app down.</summary>
    public void Exit()
    {
        if (_exiting) return;
        _exiting = true;

        _settings.Save();
        _window.Close();
        Application.Current.Shutdown();
    }

    // ------------------------------------------------------------------ updates

    /// <summary>
    /// Checks GitHub, downloads the newer exe, verifies its hash and swaps it in. Runs on a
    /// background thread; every UI touch is marshalled. Failures are silent in automatic mode
    /// so a flaky network can never turn startup into an error dialog.
    /// </summary>
    private async Task RunUpdateCheckAsync(bool automatic)
    {
        if (Interlocked.Exchange(ref _updateRunning, 1) == 1) return;

        Version current = typeof(AppController).Assembly.GetName().Version ?? new Version(1, 0);

        try
        {
            UpdateInfo? update = await _updates.CheckAsync(current).ConfigureAwait(false);
            if (update is null)
            {
                if (!automatic) Report("ISM is up to date", $"You are running v{Format(current)}.");
                return;
            }

            if (!UpdateApplier.CanSelfUpdate())
            {
                if (!automatic)
                {
                    Report(
                        "Update available",
                        $"v{Format(update.Version)} is on GitHub. Download it from the Releases page.");
                }

                return;
            }

            if (!automatic && !ConfirmUpdate(update, current)) return;

            if (!automatic) Report("Updating ISM", $"Downloading v{Format(update.Version)}...");

            string exe = Environment.ProcessPath!;
            UpdateDownloadResult download = await _updates
                .DownloadAsync(update, UpdateApplier.StagedPathFor(exe))
                .ConfigureAwait(false);

            if (!download.Ok)
            {
                if (!automatic) Report("ISM update failed", download.Error ?? "Unknown error.", warning: true);
                return;
            }

            if (!UpdateApplier.Apply(UpdateApplier.StagedPathFor(exe), out string error))
            {
                if (!automatic) Report("ISM update failed", error, warning: true);
                return;
            }

            // The hidden helper waits for this process to exit, then starts the new build.
            OnUi(() =>
            {
                _tray.ShowBalloon("ISM updated", $"Restarting with v{Format(update.Version)}...");
                Exit();
            });
        }
        catch
        {
            // Never let an update problem break the monitor.
        }
        finally
        {
            Interlocked.Exchange(ref _updateRunning, 0);
        }
    }

    private bool ConfirmUpdate(UpdateInfo update, Version current) => OnUi(() =>
        MessageBox.Show(
            "A new version of ISM is available.\n\n"
            + $"  installed:  v{Format(current)}\n"
            + $"  available:  {update.Tag}\n\n"
            + "Download it now and restart ISM?",
            "Update - ISM Internet Speed Monitor",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes);

    private void Report(string title, string text, bool warning = false) =>
        OnUi(() => _tray.ShowBalloon(title, text, warning));

    private static string Format(Version version) => version.ToString(3);

    private void OnUi(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    private T OnUi<T>(Func<T> func) => _dispatcher.CheckAccess() ? func() : _dispatcher.Invoke(func);
}
