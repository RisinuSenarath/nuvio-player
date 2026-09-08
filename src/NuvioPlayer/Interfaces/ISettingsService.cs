using NuvioPlayer.Models;

namespace NuvioPlayer.Interfaces;

public interface ISettingsService
{
    UserSettings Settings { get; }
    Task LoadAsync();
    Task SaveAsync();
    void ResetToDefaults();
    string GetAppDataPath();
}
