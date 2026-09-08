using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;

namespace NuvioPlayer.Services;

public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsFilePath;
    private readonly string _appDataPath;
    private UserSettings _settings = new();

    public UserSettings Settings => _settings;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NuvioPlayer");

        Directory.CreateDirectory(_appDataPath);
        _settingsFilePath = Path.Combine(_appDataPath, "settings.json");

        string defaultScreenshots = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Nuvio Screenshots");
        _settings.ScreenshotDirectory = defaultScreenshots;
    }

    public string GetAppDataPath() => _appDataPath;

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<UserSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                    if (string.IsNullOrWhiteSpace(_settings.ScreenshotDirectory))
                    {
                        _settings.ScreenshotDirectory = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                            "Nuvio Screenshots");
                    }
                    _logger.LogInformation("User settings successfully loaded from {Path}", _settingsFilePath);
                    return;
                }
            }
            _logger.LogInformation("No existing settings file found; using default settings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}; falling back to defaults", _settingsFilePath);
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            await File.WriteAllTextAsync(_settingsFilePath, json);
            _logger.LogInformation("User settings saved to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _settingsFilePath);
        }
    }

    public void ResetToDefaults()
    {
        _settings = new UserSettings
        {
            ScreenshotDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Nuvio Screenshots")
        };
        _logger.LogInformation("Settings reset to defaults");
    }
}
