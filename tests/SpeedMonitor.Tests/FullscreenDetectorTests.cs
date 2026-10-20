using SpeedMonitor.Services;

namespace SpeedMonitor.Tests;

public sealed class FullscreenDetectorTests
{
    private static readonly RectPx Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void Window_that_exactly_covers_the_monitor_is_fullscreen()
    {
        Assert.True(FullscreenDetector.CoversMonitor(Monitor, Monitor));
    }

    [Fact]
    public void Maximised_window_above_the_taskbar_is_not_fullscreen()
    {
        // A maximised window stops at the work area: the 48 px taskbar is still visible.
        var maximised = new RectPx(0, 0, 1920, 1032);

        Assert.False(FullscreenDetector.CoversMonitor(maximised, Monitor));
    }

    [Fact]
    public void Window_larger_than_the_monitor_is_fullscreen()
    {
        // Some windows report an invisible resize border around the screen.
        var oversized = new RectPx(-8, -8, 1928, 1088);

        Assert.True(FullscreenDetector.CoversMonitor(oversized, Monitor));
    }

    [Fact]
    public void Borders_within_the_tolerance_still_count_as_fullscreen()
    {
        var onePixelShort = new RectPx(0, 0, 1919, 1079);

        Assert.True(FullscreenDetector.CoversMonitor(onePixelShort, Monitor));
    }

    [Fact]
    public void Window_that_leaves_room_on_any_edge_is_not_fullscreen()
    {
        Assert.False(FullscreenDetector.CoversMonitor(new RectPx(0, 0, 1920, 1040), Monitor));
        Assert.False(FullscreenDetector.CoversMonitor(new RectPx(0, 30, 1920, 1080), Monitor));
        Assert.False(FullscreenDetector.CoversMonitor(new RectPx(0, 0, 1600, 1080), Monitor));
    }

    [Fact]
    public void Second_monitor_uses_its_own_origin()
    {
        var secondary = new RectPx(1920, 0, 3840, 1080);
        var fullscreenOnSecondary = new RectPx(1920, 0, 3840, 1080);

        Assert.True(FullscreenDetector.CoversMonitor(fullscreenOnSecondary, secondary));
        Assert.False(FullscreenDetector.CoversMonitor(fullscreenOnSecondary, Monitor));
    }
}
