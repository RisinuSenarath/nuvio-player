using Microsoft.Extensions.Logging.Abstractions;
using NuvioPlayer.Services;
using Xunit;

namespace NuvioPlayer.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void DefaultSettings_ShouldHaveReasonableValues()
    {
        var service = new SettingsService(NullLogger<SettingsService>.Instance);
        var settings = service.Settings;

        Assert.NotNull(settings);
        Assert.True(settings.ResumePlayback);
        Assert.True(settings.RememberWindowSize);
        Assert.True(settings.RememberWindowPosition);
        Assert.Equal(100, settings.Volume);
        Assert.Equal(1.0, settings.DefaultPlaybackSpeed);
        Assert.Equal(5, settings.ShortSeekSeconds);
        Assert.Equal(30, settings.LongSeekSeconds);
        Assert.False(string.IsNullOrWhiteSpace(settings.ScreenshotDirectory));
    }

    [Fact]
    public void ResetToDefaults_ShouldRestoreDefaultValues()
    {
        var service = new SettingsService(NullLogger<SettingsService>.Instance);
        service.Settings.Volume = 20;
        service.Settings.DefaultPlaybackSpeed = 2.0;

        service.ResetToDefaults();

        Assert.Equal(100, service.Settings.Volume);
        Assert.Equal(1.0, service.Settings.DefaultPlaybackSpeed);
    }

    [Fact]
    public async Task SaveAndLoad_ShouldRoundtripSettings()
    {
        var service = new SettingsService(NullLogger<SettingsService>.Instance);
        service.Settings.Volume = 45;
        service.Settings.DefaultPlaybackSpeed = 1.25;
        service.Settings.Theme = "Dark";

        await service.SaveAsync();

        var newService = new SettingsService(NullLogger<SettingsService>.Instance);
        await newService.LoadAsync();

        Assert.Equal(45, newService.Settings.Volume);
        Assert.Equal(1.25, newService.Settings.DefaultPlaybackSpeed);
    }
}
