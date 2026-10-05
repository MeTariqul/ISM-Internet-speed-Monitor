using System.IO;
using System.Windows;
using SpeedMonitor.Metrics;
using SpeedMonitor.Services;
using SpeedMonitor.ViewModels;

namespace SpeedMonitor;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\SpeedMonitor.SingleInstance";
    private const string SignalName = @"Local\SpeedMonitor.ShowSignal";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private TaskbarWindow? _window;
    private TaskbarMenu? _menu;
    private TrayIconService? _tray;
    private bool _exiting;

    public AppSettings Settings { get; } = AppSettings.Load();
    public MetricsSampler Sampler { get; private set; } = null!;
    public MonitorViewModel ViewModel { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Install/uninstall must run before the single-instance logic: the "Settings → Apps"
        // uninstaller and `exe /install` may start while another copy is already running.
        if (HandleCommandLine(e.Args))
        {
            Shutdown();
            return;
        }

        _instanceMutex = new Mutex(true, MutexName, out bool firstInstance);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName, out _);

        if (!firstInstance)
        {
            // A second launch just asks the running instance to show its menu.
            try { _showSignal.Set(); } catch { /* the other instance may have exited */ }
            Shutdown();
            return;
        }

        ThreadPool.RegisterWaitForSingleObject(
            _showSignal,
            (_, _) => Dispatcher.BeginInvoke(new Action(ShowMenu)),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        Sampler = new MetricsSampler(Settings.Adapter);
        ViewModel = new MonitorViewModel(Settings);
        Sampler.Sampled += OnSampled;
        Sampler.Start();

        _menu = new TaskbarMenu(
            Settings,
            ViewModel,
            setAdapter: adapter => Sampler.SetAdapter(adapter),
            applyTextVisibility: visible => _window?.SetOverlayVisible(visible),
            exit: ExitApp);

        _window = new TaskbarWindow(ViewModel, _menu, Settings.ShowTaskbarText);
        if (Settings.ShowTaskbarText)
        {
            _window.Show();
        }

        _tray = new TrayIconService(_menu.Menu, ShowMenu);
        _tray.SetText(ViewModel.TrayText);
    }

    private void OnSampled(Sample sample)
    {
        // Raised on the sampler thread; marshal everything to the UI thread.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ViewModel.AddSample(sample);
            _tray?.SetText(ViewModel.TrayText);
        }));
    }

    private void ShowMenu()
    {
        _menu?.ShowAt(System.Windows.Forms.Cursor.Position);
    }

    /// <summary>Handles /install, /uninstall and /help. Returns true when the args were a command.</summary>
    private bool HandleCommandLine(string[] args)
    {
        bool quiet = HasFlag(args, "/quiet", "-quiet", "--quiet", "/S", "-s");

        if (HasFlag(args, "/install", "-install", "--install"))
        {
            var (ok, message) = SelfInstall.Install();
            if (!quiet)
            {
                MessageBox.Show(
                    ok
                        ? $"Installed.\n\n{message}\n\n"
                          + "\u2022 Start Menu shortcut created\n"
                          + "\u2022 Uninstall entry added (Settings > Apps)\n\n"
                          + "Start it from the Start Menu from now on; uninstall any time from\n"
                          + "the tray menu or from Settings > Apps."
                        : "Install failed:\n\n" + message,
                    "ISM - Internet Speed Monitor",
                    MessageBoxButton.OK,
                    ok ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            return true;
        }

        if (HasFlag(args, "/uninstall", "-uninstall", "--uninstall"))
        {
            var (ok, message) = SelfInstall.Uninstall(deleteSettings: true);
            if (!quiet)
            {
                MessageBox.Show(
                    ok
                        ? "Uninstalled - removed:\n\n"
                          + "\u2022 Start Menu shortcut\n"
                          + "\u2022 Settings > Apps entry\n"
                          + "\u2022 the installed copy\n"
                          + "\u2022 your settings\n"
                        : "Uninstall failed:\n\n" + message,
                    "ISM - Internet Speed Monitor",
                    MessageBoxButton.OK,
                    ok ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            return true;
        }

        if (HasFlag(args, "/?", "-?", "/help", "-h", "--help"))
        {
            MessageBox.Show(
                "ISM - Internet Speed Monitor\n\n"
                + "  (no arguments)   run the monitor\n"
                + "  /install         install for this user (Start Menu + Apps entry)\n"
                + "  /uninstall       remove the installation and its settings\n"
                + "  /quiet           no dialogs (silent install / uninstall)\n",
                "ISM - Internet Speed Monitor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true;
        }

        return false;
    }

    private static bool HasFlag(string[] args, params string[] names) =>
        args.Any(a => names.Contains(a, StringComparer.OrdinalIgnoreCase));

    public void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;

        Settings.Save();

        _tray?.Dispose();
        _tray = null;
        _menu?.Dispose();
        _menu = null;

        if (_window is not null)
        {
            _window.Close();
            _window = null;
        }

        Sampler.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showSignal?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpeedMonitor");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{e.Exception}{Environment.NewLine}");
        }
        catch { /* logging must never throw */ }
    }
}
