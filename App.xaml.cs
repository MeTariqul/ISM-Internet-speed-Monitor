using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SpeedMonitor.Services;

namespace SpeedMonitor;

/// <summary>
/// Process-level entry point: command-line handling, single-instance signalling, the
/// licensing gate and building the DI container. Runtime wiring lives in
/// <see cref="AppController"/>.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\SpeedMonitor.SingleInstance";
    private const string SignalName = @"Local\SpeedMonitor.ShowSignal";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private ServiceProvider? _services;
    private AppController? _controller;

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
            (_, _) => Dispatcher.BeginInvoke(new Action(() => _controller?.ShowMenu())),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _services = CompositionRoot.Build();

        var license = _services.GetRequiredService<ILicenseService>();
        var dialogs = _services.GetRequiredService<LicenseDialogService>();

        // Hard block: no valid license, no monitor. /install and /uninstall still work above.
        if (!EnsureLicensed(license, dialogs))
        {
            Shutdown();
            return;
        }

        _controller = _services.GetRequiredService<AppController>();
        _controller.Start();
    }

    /// <summary>
    /// Returns true when the app may run. The stored token is verified offline; when the token
    /// asks for it (or the app was never activated) the server is consulted or the activation
    /// dialog is shown. Cancelling the dialog means exit.
    /// </summary>
    private static bool EnsureLicensed(ILicenseService license, LicenseDialogService dialogs)
    {
        LicenseResult offline = license.ValidateOffline();

        if (offline.Ok && license.NeedsRecheck)
        {
            // Off the UI thread so the awaits cannot dead-lock the starter; this only runs
            // when the token's re-check time has passed.
            try
            {
                Task.Run(() => license.VerifyAsync()).GetAwaiter().GetResult();
            }
            catch
            {
                // An unexpected client failure is treated like a network outage below.
            }

            // The signed token is the source of truth. VerifyAsync only deletes it for a
            // definite revocation (suspended / expired / revoked / device removed); any other
            // server failure (outage, misconfigured app, "Unknown app.") leaves it intact, so a
            // still-valid token keeps the app running.
            offline = license.ValidateOffline();
        }

        if (offline.Ok) return true;

        return dialogs.ShowActivation(offline.Error);
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

    protected override void OnExit(ExitEventArgs e)
    {
        _controller = null;

        // Disposes the singleton services (sampler, tray icon, menu) in reverse order.
        _services?.Dispose();
        _services = null;

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
