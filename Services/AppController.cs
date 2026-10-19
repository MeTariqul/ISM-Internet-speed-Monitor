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
    private readonly Dispatcher _dispatcher;

    private bool _started;
    private bool _exiting;

    public AppController(
        AppSettings settings,
        MetricsSampler sampler,
        MonitorViewModel viewModel,
        TaskbarMenu menu,
        TaskbarWindow window,
        TrayIconService tray,
        Dispatcher dispatcher)
    {
        _settings = settings;
        _sampler = sampler;
        _viewModel = viewModel;
        _menu = menu;
        _window = window;
        _tray = tray;
        _dispatcher = dispatcher;

        _window.MenuRequested += (_, _) => ShowMenu();
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
    }

    /// <summary>Opens the shared settings menu at the current cursor position.</summary>
    public void ShowMenu() => _menu.ShowAt(System.Windows.Forms.Cursor.Position);

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
}
