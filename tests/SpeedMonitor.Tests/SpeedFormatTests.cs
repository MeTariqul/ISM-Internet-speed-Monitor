using System.Globalization;
using SpeedMonitor.Metrics;

namespace SpeedMonitor.Tests;

public sealed class SpeedFormatTests
{
    public SpeedFormatTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    [Fact]
    public void Unavailable_values_render_as_an_em_dash()
    {
        Assert.Equal("\u2014", SpeedFormat.Format(double.NaN, UnitMode.Auto));
        Assert.Equal("\u2014", SpeedFormat.Format(-1, UnitMode.Auto));
        Assert.Equal("\u2014", SpeedFormat.Compact(double.NaN, UnitMode.Auto));
    }

    [Theory]
    [InlineData(0d, "0 B/s")]
    [InlineData(512d, "512 B/s")]
    [InlineData(1024d, "1.0 KB/s")]
    [InlineData(1_048_576d, "1.00 MB/s")]
    [InlineData(1_073_741_824d, "1.00 GB/s")]
    public void Auto_mode_scales_by_binary_units(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, SpeedFormat.Format(bytesPerSecond, UnitMode.Auto));
    }

    [Fact]
    public void Kilobytes_mode_keeps_small_rates_in_kilobytes()
    {
        Assert.Equal("0.5 KB/s", SpeedFormat.Format(512, UnitMode.KiloBytes));

        // Large values still promote so the overlay never shows "1024.0 KB/s".
        Assert.Equal("1.00 MB/s", SpeedFormat.Format(1_048_576, UnitMode.KiloBytes));
    }

    [Theory]
    [InlineData(1_000_000d, "8.0 Mbps")]
    [InlineData(12_500_000d, "100 Mbps")]
    public void Megabits_mode_uses_decimal_megabits(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, SpeedFormat.Format(bytesPerSecond, UnitMode.Megabits));
    }

    [Theory]
    [InlineData(0d, "0B")]
    [InlineData(1024d, "1K")]
    [InlineData(2_097_152d, "2.0M")]
    [InlineData(1_610_612_736d, "1.50G")]
    public void Compact_labels_are_short(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, SpeedFormat.Compact(bytesPerSecond, UnitMode.Auto));
    }

    [Theory]
    [InlineData(0d, 1d)]
    [InlineData(1d, 1d)]
    [InlineData(7d, 10d)]
    [InlineData(120d, 200d)]
    [InlineData(3d, 5d)]
    [InlineData(double.NaN, 1d)]
    public void NiceMax_rounds_up_to_1_2_or_5(double value, double expected)
    {
        Assert.Equal(expected, SpeedFormat.NiceMax(value));
    }
}
