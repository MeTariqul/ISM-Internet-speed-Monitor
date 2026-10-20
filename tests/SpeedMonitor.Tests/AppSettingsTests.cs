using SpeedMonitor.Metrics;
using SpeedMonitor.Services;

namespace SpeedMonitor.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void New_settings_have_safe_defaults()
    {
        var settings = new AppSettings();

        Assert.Equal("Auto", settings.Units);
        Assert.Equal("Network", settings.TaskbarFormat);
        Assert.True(settings.ShowTaskbarText);
        Assert.False(settings.ShowSystem);
        Assert.False(settings.StartWithWindows);
        Assert.Null(settings.Adapter);
        Assert.True(settings.AutoUpdate);
        Assert.Equal(UnitMode.Auto, settings.UnitMode);
    }

    [Fact]
    public void Json_round_trip_preserves_every_persisted_value()
    {
        var original = new AppSettings
        {
            Units = "Mbps",
            Adapter = "Intel(R) Wi-Fi 6E AX211",
            StartWithWindows = true,
            TaskbarFormat = "Both",
            ShowSystem = true,
            ShowTaskbarText = false,
            AutoUpdate = false,
        };

        string json = original.ToJson();
        AppSettings? restored = AppSettings.FromJson(json);

        Assert.NotNull(restored);
        Assert.Equal(original.Units, restored!.Units);
        Assert.Equal(original.Adapter, restored.Adapter);
        Assert.Equal(original.StartWithWindows, restored.StartWithWindows);
        Assert.Equal(original.TaskbarFormat, restored.TaskbarFormat);
        Assert.Equal(original.ShowSystem, restored.ShowSystem);
        Assert.Equal(original.ShowTaskbarText, restored.ShowTaskbarText);
        Assert.Equal(original.AutoUpdate, restored.AutoUpdate);
    }

    [Fact]
    public void Derived_unit_mode_is_not_serialized_but_is_recomputed()
    {
        string json = new AppSettings { Units = "KB" }.ToJson();

        Assert.DoesNotContain("UnitMode", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UnitMode.KiloBytes, AppSettings.FromJson(json)!.UnitMode);
    }

    [Fact]
    public void Deserialization_is_case_insensitive()
    {
        AppSettings? settings = AppSettings.FromJson("""{"units":"Mbps","taskbarFormat":"Disk"}""");

        Assert.NotNull(settings);
        Assert.Equal("Mbps", settings!.Units);
        Assert.Equal(UnitMode.Megabits, settings.UnitMode);
        Assert.Equal("Disk", settings.TaskbarFormat);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{\"Units\":")]
    public void Invalid_json_returns_null_instead_of_throwing(string? json)
    {
        Assert.Null(AppSettings.FromJson(json));
    }

    [Fact]
    public void Empty_object_yields_defaults()
    {
        AppSettings? settings = AppSettings.FromJson("{}");

        Assert.NotNull(settings);
        Assert.Equal("Auto", settings!.Units);
        Assert.Equal("Network", settings.TaskbarFormat);
    }

    [Fact]
    public void Missing_properties_keep_their_defaults()
    {
        AppSettings? settings = AppSettings.FromJson("""{"ShowSystem":true}""");

        Assert.NotNull(settings);
        Assert.True(settings!.ShowSystem);
        Assert.True(settings.ShowTaskbarText);
        Assert.True(settings.AutoUpdate);
        Assert.Equal("Network", settings.TaskbarFormat);
    }
}
