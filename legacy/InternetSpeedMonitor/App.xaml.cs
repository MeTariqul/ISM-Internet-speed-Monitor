using System.Windows;
using System.Diagnostics;

namespace InternetSpeedMonitor;

/// <summary>
/// Application entry point - ensures single instance and handles startup/shutdown
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "InternetSpeedMonitor_SingleInstance";
    private bool _isMutexOwned = false;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        // Ensure single instance
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        _isMutexOwned = createdNew;
        
        if (!createdNew)
        {
            // Already running - exit
            MessageBox.Show("Internet Speed Monitor is already running!", 
                "Already Running", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
            Current.Shutdown();
            return;
        }
        
        // Set up global exception handling
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Debug.WriteLine($"Unhandled exception: {ex?.Message}");
        };
        
        DispatcherUnhandledException += (s, args) =>
        {
            Debug.WriteLine($"Dispatcher exception: {args.Exception.Message}");
            args.Handled = true;
        };
        
        base.OnStartup(e);
    }
    
    protected override void OnExit(ExitEventArgs e)
    {
        if (_isMutexOwned)
        {
            _mutex?.ReleaseMutex();
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
