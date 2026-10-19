using System.Globalization;
using SpeedMonitor.Metrics;
using SpeedMonitor.Services;
using SpeedMonitor.ViewModels;

namespace SpeedMonitor.Tests;

public sealed class MonitorViewModelTests
{
    public MonitorViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    private static Sample Reading(
        double down = 1_048_576d,
        double up = 512_000d,
        double diskRead = 0d,
        double diskWrite = 0d,
        double cpu = 12.6d,
        double memory = 63.6d)
        => new(DateTime.UtcNow, down, up, diskRead, diskWrite, cpu, memory, 8000d);

    [Fact]
    public void Starts_with_placeholders()
    {
        var viewModel = new MonitorViewModel(new AppSettings());

        Assert.Equal("\u2026", viewModel.TaskbarText);
        Assert.Equal("Speed Monitor", viewModel.TrayText);
    }

    [Fact]
    public void Sample_is_formatted_as_upload_then_download()
    {
        var viewModel = new MonitorViewModel(new AppSettings());

        viewModel.AddSample(Reading());

        Assert.Equal("\u2191 500.0 KB/s   \u2193 1.00 MB/s", viewModel.TaskbarText);
        Assert.Equal("ISM " + viewModel.TaskbarText, viewModel.TrayText);
    }

    [Fact]
    public void Changing_units_updates_the_view_model_and_the_settings()
    {
        var settings = new AppSettings();
        var viewModel = new MonitorViewModel(settings);
        viewModel.AddSample(Reading());

        viewModel.Units = UnitMode.Megabits;

        Assert.Equal("Mbps", settings.Units);
        Assert.Contains("Mbps", viewModel.TaskbarText);
        Assert.Equal(UnitMode.Megabits, viewModel.Units);
    }

    [Fact]
    public void System_stats_are_appended_when_enabled()
    {
        var settings = new AppSettings { ShowSystem = true };
        var viewModel = new MonitorViewModel(settings);

        viewModel.AddSample(Reading());

        Assert.Contains("CPU 13%", viewModel.TaskbarText);
        Assert.Contains("RAM 64%", viewModel.TaskbarText);
    }

    [Fact]
    public void Disk_format_omits_network_rates()
    {
        var settings = new AppSettings { TaskbarFormat = "Disk" };
        var viewModel = new MonitorViewModel(settings);

        viewModel.AddSample(Reading(diskRead: 2_097_152d, diskWrite: 1_048_576d));

        Assert.Equal("R 2.00 MB/s   W 1.00 MB/s", viewModel.TaskbarText);
    }

    [Fact]
    public void Unavailable_readings_render_as_em_dash()
    {
        var settings = new AppSettings { ShowSystem = true };
        var viewModel = new MonitorViewModel(settings);

        viewModel.AddSample(Reading(down: double.NaN, up: double.NaN, cpu: double.NaN, memory: double.NaN));

        Assert.Contains("\u2014", viewModel.TaskbarText);
        Assert.Contains("CPU \u2014", viewModel.TaskbarText);
        Assert.Contains("RAM \u2014", viewModel.TaskbarText);
    }

    [Fact]
    public void Refresh_command_is_available_and_recomputes_text()
    {
        var viewModel = new MonitorViewModel(new AppSettings());

        Assert.True(viewModel.RefreshCommand.CanExecute(null));

        viewModel.AddSample(Reading());
        viewModel.RefreshCommand.Execute(null);

        Assert.Contains("\u2193 1.00 MB/s", viewModel.TaskbarText);
    }
}
