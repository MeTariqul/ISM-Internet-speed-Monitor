using SpeedMonitor.Metrics;

namespace SpeedMonitor.Tests;

public sealed class BandwidthMathTests
{
    [Fact]
    public void Converts_bytes_per_second_to_decimal_megabits()
    {
        Assert.Equal(8d, BandwidthMath.ToMegabitsPerSecond(1_000_000d), 6);
        Assert.Equal(0.8d, BandwidthMath.ToMegabitsPerSecond(100_000d), 6);
    }

    [Fact]
    public void Invalid_rates_stay_unavailable()
    {
        Assert.True(double.IsNaN(BandwidthMath.ToMegabitsPerSecond(double.NaN)));
        Assert.True(double.IsNaN(BandwidthMath.ToMegabitsPerSecond(-5d)));
    }

    [Fact]
    public void Converts_bytes_to_mebibytes()
    {
        Assert.Equal(1d, BandwidthMath.ToMebibytes(1_048_576d), 6);
        Assert.Equal(0.5d, BandwidthMath.ToMebibytes(524_288d), 6);
    }

    [Theory]
    [InlineData(16d, 4d, 75d)]
    [InlineData(16d, 16d, 0d)]
    [InlineData(16d, 0d, 100d)]
    [InlineData(16d, 20d, 0d)] // available > total is clamped, never negative
    [InlineData(0d, 0d, double.NaN)]
    [InlineData(double.NaN, 1d, double.NaN)]
    public void PercentUsed_is_clamped_and_safe(double total, double available, double expected)
    {
        double actual = BandwidthMath.PercentUsed(total, available);

        if (double.IsNaN(expected)) Assert.True(double.IsNaN(actual));
        else Assert.Equal(expected, actual, 6);
    }

    [Fact]
    public void Used_never_returns_a_negative_value()
    {
        Assert.Equal(12d, BandwidthMath.Used(16d, 4d), 6);
        Assert.Equal(0d, BandwidthMath.Used(16d, 20d), 6);
        Assert.True(double.IsNaN(BandwidthMath.Used(double.NaN, 4d)));
    }
}
