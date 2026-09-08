using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;

namespace NuvioPlayer.ViewModels;

public record ShortcutItem(string Action, string Shortcut);

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IDatabaseService _databaseService;
    private readonly IFileAssociationService? _fileAssociationService;

    [ObservableProperty]
    private string _fileAssociationStatusMessage = string.Empty;

    [ObservableProperty]
    private int _selectedSectionIndex = 0;

    // General
    [ObservableProperty]
    private bool _resumePlayback;

    [ObservableProperty]
    private bool _rememberWindowSize;

    [ObservableProperty]
    private bool _rememberWindowPosition;

    // Playback
    [ObservableProperty]
    private double _defaultPlaybackSpeed;

    [ObservableProperty]
    private int _shortSeekSeconds;

    [ObservableProperty]
    private int _longSeekSeconds;

    [ObservableProperty]
    private bool _autoPlayNext;

    [ObservableProperty]
    private string _aspectRatio = string.Empty;

    // Interface
    [ObservableProperty]
    private string _theme = string.Empty;

    [ObservableProperty]
    private int _controlsAutoHideDelaySeconds;

    [ObservableProperty]
    private bool _enableAnimations;

    // Subtitles
    [ObservableProperty]
    private bool _autoLoadSubtitles;

    [ObservableProperty]
    private int _subtitleFontSize;

    [ObservableProperty]
    private string _subtitleColor = string.Empty;

    // Audio
    [ObservableProperty]
    private int _defaultVolume;

    // Files & Screenshots
    [ObservableProperty]
    private string _screenshotDirectory = string.Empty;

    // Privacy
    [ObservableProperty]
    private bool _recordPlaybackHistory;

    // Advanced
    [ObservableProperty]
    private bool _hardwareAcceleration;

    [ObservableProperty]
    private string _logLevel = string.Empty;

    [ObservableProperty]
    private string _mediaEngineInfo = string.Empty;

    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();
    public ObservableCollection<string> SupportedExtensions { get; } = new();

    public event Action? RequestClose;
    public event Action<string>? ShowMessage;
    public Func<string, string, bool>? ConfirmDialog { get; set; }

    private bool AskConfirmation(string message, string title)
    {
        if (ConfirmDialog != null)
            return ConfirmDialog(message, title);
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        ISettingsService settingsService,
        IDatabaseService databaseService,
        IFileAssociationService? fileAssociationService = null)
    {
        _logger = logger;
        _settingsService = settingsService;
        _databaseService = databaseService;
        _fileAssociationService = fileAssociationService;

        LoadCurrentSettings();
        InitializeShortcuts();
        InitializeExtensions();
        InitializeEngineInfo();
    }

    private void LoadCurrentSettings()
    {
        var s = _settingsService.Settings;
        ResumePlayback = s.ResumePlayback;
        RememberWindowSize = s.RememberWindowSize;
        RememberWindowPosition = s.RememberWindowPosition;

        DefaultPlaybackSpeed = s.DefaultPlaybackSpeed;
        ShortSeekSeconds = s.ShortSeekSeconds;
        LongSeekSeconds = s.LongSeekSeconds;
        AutoPlayNext = s.AutoPlayNext;
        AspectRatio = s.AspectRatio;

        Theme = s.Theme;
        ControlsAutoHideDelaySeconds = s.ControlsAutoHideDelaySeconds;
        EnableAnimations = s.EnableAnimations;

        AutoLoadSubtitles = s.AutoLoadSubtitles;
        SubtitleFontSize = s.SubtitleFontSize;
        SubtitleColor = s.SubtitleColor;

        DefaultVolume = s.Volume;
        ScreenshotDirectory = s.ScreenshotDirectory;

        RecordPlaybackHistory = s.RecordPlaybackHistory;

        HardwareAcceleration = s.HardwareAcceleration;
        LogLevel = s.LogLevel;
    }

    private void InitializeShortcuts()
    {
        Shortcuts.Clear();
        Shortcuts.Add(new ShortcutItem("Play / Pause", "Space"));
        Shortcuts.Add(new ShortcutItem("Seek Backward (Short)", "Left Arrow (5s)"));
        Shortcuts.Add(new ShortcutItem("Seek Forward (Short)", "Right Arrow (5s)"));
        Shortcuts.Add(new ShortcutItem("Seek Backward (Long)", "Shift + Left Arrow (30s)"));
        Shortcuts.Add(new ShortcutItem("Seek Forward (Long)", "Shift + Right Arrow (30s)"));
        Shortcuts.Add(new ShortcutItem("Volume Up", "Up Arrow / Wheel Up"));
        Shortcuts.Add(new ShortcutItem("Volume Down", "Down Arrow / Wheel Down"));
        Shortcuts.Add(new ShortcutItem("Mute / Unmute", "M"));
        Shortcuts.Add(new ShortcutItem("Toggle Fullscreen", "F / Double Click"));
        Shortcuts.Add(new ShortcutItem("Exit Fullscreen", "Esc"));
        Shortcuts.Add(new ShortcutItem("Next Media", "N"));
        Shortcuts.Add(new ShortcutItem("Previous Media", "P"));
        Shortcuts.Add(new ShortcutItem("Toggle Subtitles", "S"));
        Shortcuts.Add(new ShortcutItem("Subtitle Delay (-50ms)", "G"));
        Shortcuts.Add(new ShortcutItem("Subtitle Delay (+50ms)", "H"));
        Shortcuts.Add(new ShortcutItem("Audio Delay (-50ms)", "J"));
        Shortcuts.Add(new ShortcutItem("Audio Delay (+50ms)", "K"));
        Shortcuts.Add(new ShortcutItem("Take Screenshot", "F12"));
        Shortcuts.Add(new ShortcutItem("Media Information", "I"));
        Shortcuts.Add(new ShortcutItem("Open File", "Ctrl + O / O"));
        Shortcuts.Add(new ShortcutItem("Open Folder", "Ctrl + Shift + O"));
        Shortcuts.Add(new ShortcutItem("Toggle Playlist", "Ctrl + P"));
    }

    private void InitializeExtensions()
    {
        SupportedExtensions.Clear();
        foreach (var ext in NuvioPlayer.Helpers.MediaFileHelper.AllSupportedMediaExtensions)
        {
            SupportedExtensions.Add(ext);
        }
    }

    private void InitializeEngineInfo()
    {
        string arch = Environment.Is64BitProcess ? "x64" : "x86";
        string os = Environment.OSVersion.VersionString;
        string netVer = Environment.Version.ToString();
        MediaEngineInfo = $"Media Engine: LibVLC 3.x (Bundled Windows Runtime)\nArchitecture: {arch}\nFramework: .NET {netVer}\nOperating System: {os}";
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        var s = _settingsService.Settings;
        s.ResumePlayback = ResumePlayback;
        s.RememberWindowSize = RememberWindowSize;
        s.RememberWindowPosition = RememberWindowPosition;

        s.DefaultPlaybackSpeed = DefaultPlaybackSpeed;
        s.ShortSeekSeconds = ShortSeekSeconds;
        s.LongSeekSeconds = LongSeekSeconds;
        s.AutoPlayNext = AutoPlayNext;
        s.AspectRatio = AspectRatio;

        s.Theme = Theme;
        s.ControlsAutoHideDelaySeconds = ControlsAutoHideDelaySeconds;
        s.EnableAnimations = EnableAnimations;

        s.AutoLoadSubtitles = AutoLoadSubtitles;
        s.SubtitleFontSize = SubtitleFontSize;
        s.SubtitleColor = SubtitleColor;

        s.Volume = DefaultVolume;
        s.ScreenshotDirectory = ScreenshotDirectory;

        s.RecordPlaybackHistory = RecordPlaybackHistory;

        s.HardwareAcceleration = HardwareAcceleration;
        s.LogLevel = LogLevel;

        await _settingsService.SaveAsync();
        _logger.LogInformation("Settings saved from SettingsViewModel");
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void BrowseScreenshotDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Screenshot Directory",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && Directory.Exists(dialog.FolderName))
        {
            ScreenshotDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    public async Task RegisterAllAssociationsAsync()
    {
        if (_fileAssociationService == null)
        {
            FileAssociationStatusMessage = "File association service is unavailable.";
            return;
        }

        FileAssociationStatusMessage = "Registering file associations with Windows...";
        bool success = await _fileAssociationService.RegisterAllAssociationsAsync();
        FileAssociationStatusMessage = success
            ? "Supported media formats successfully registered with Nuvio Player."
            : "Some file associations could not be updated.";
        ShowMessage?.Invoke(FileAssociationStatusMessage);
    }

    [RelayCommand]
    public async Task UnregisterAllAssociationsAsync()
    {
        if (_fileAssociationService == null)
        {
            FileAssociationStatusMessage = "File association service is unavailable.";
            return;
        }

        FileAssociationStatusMessage = "Unregistering file associations...";
        bool success = await _fileAssociationService.UnregisterAllAssociationsAsync();
        FileAssociationStatusMessage = success
            ? "Nuvio Player associations removed from current user."
            : "Some associations could not be removed.";
        ShowMessage?.Invoke(FileAssociationStatusMessage);
    }

    [RelayCommand]
    public void ResetSettings()
    {
        if (AskConfirmation("Are you sure you want to reset all settings to their default values?", "Reset Settings — Nuvio Player"))
        {
            _settingsService.ResetToDefaults();
            LoadCurrentSettings();
            ShowMessage?.Invoke("Settings have been reset to defaults.");
        }
    }

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        if (AskConfirmation("Are you sure you want to clear all playback history?", "Clear History — Nuvio Player"))
        {
            await _databaseService.ClearHistoryAsync();
            ShowMessage?.Invoke("Playback history cleared successfully.");
        }
    }

    [RelayCommand]
    public async Task ClearBookmarksAsync()
    {
        if (AskConfirmation("Are you sure you want to clear all saved bookmarks across all media?", "Clear Bookmarks — Nuvio Player"))
        {
            await _databaseService.ClearBookmarksAsync();
            ShowMessage?.Invoke("All bookmarks cleared successfully.");
        }
    }

    [RelayCommand]
    public async Task ClearPlaylistsAsync()
    {
        if (AskConfirmation("Are you sure you want to clear all saved playlists?", "Clear Playlists — Nuvio Player"))
        {
            await _databaseService.ClearPlaylistsAsync();
            ShowMessage?.Invoke("All playlists cleared successfully.");
        }
    }
}
