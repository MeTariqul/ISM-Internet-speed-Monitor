using Microsoft.Extensions.DependencyInjection;
using SpeedMonitor.Metrics;
using SpeedMonitor.ViewModels;

namespace SpeedMonitor.Services;

/// <summary>
/// The single composition root. Kept outside <see cref="App"/> so the container can be
/// built and validated by tests or a smoke harness without starting WPF.
/// </summary>
public static class CompositionRoot
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton(_ => AppSettings.Load());
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<LicenseDialogService>();
        services.AddSingleton(_ => new UpdateService());

        services.AddSingleton(sp => new MetricsSampler(sp.GetRequiredService<AppSettings>().Adapter));
        services.AddSingleton<MonitorViewModel>();

        services.AddSingleton(sp => new TaskbarWindow(
            sp.GetRequiredService<MonitorViewModel>(),
            sp.GetRequiredService<AppSettings>().ShowTaskbarText));

        // The menu needs the window (to toggle the overlay) and the controller (to exit);
        // the controller needs the menu. Resolve lazily on first invocation to break the cycle.
        services.AddSingleton(sp => new TaskbarMenu(
            sp.GetRequiredService<AppSettings>(),
            sp.GetRequiredService<MonitorViewModel>(),
            sp.GetRequiredService<MetricsSampler>(),
            sp.GetRequiredService<LicenseDialogService>(),
            visible => sp.GetRequiredService<TaskbarWindow>().SetOverlayVisible(visible),
            () => sp.GetRequiredService<AppController>().Exit(),
            () => sp.GetRequiredService<AppController>().CheckForUpdatesManually()));

        services.AddSingleton(sp => new TrayIconService(
            sp.GetRequiredService<TaskbarMenu>().Menu,
            () => sp.GetRequiredService<AppController>().ShowMenu()));

        services.AddSingleton(_ => System.Windows.Application.Current.Dispatcher);
        services.AddSingleton<AppController>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
