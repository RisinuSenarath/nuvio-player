namespace NuvioPlayer.Models;

public class UserSettings
{
    // General
    public bool ResumePlayback { get; set; } = true;
    public bool RememberWindowSize { get; set; } = true;
    public bool RememberWindowPosition { get; set; } = true;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 720;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public bool IsMaximized { get; set; } = false;

    // Playback
    public double DefaultPlaybackSpeed { get; set; } = 1.0;
    public int ShortSeekSeconds { get; set; } = 5;
    public int LongSeekSeconds { get; set; } = 30;
    public bool AutoPlayNext { get; set; } = true;
    public int Volume { get; set; } = 100;
    public bool IsMuted { get; set; } = false;
    public string AspectRatio { get; set; } = "Default"; // "Default", "16:9", "4:3", "1:1", "21:9", "Fit", "Fill"

    // Subtitles
    public bool AutoLoadSubtitles { get; set; } = true;
    public int SubtitleFontSize { get; set; } = 28;
    public string SubtitleColor { get; set; } = "#FFFFFF";
    public int SubtitleDelayMs { get; set; } = 0;

    // Audio
    public int AudioDelayMs { get; set; } = 0;

    // Interface
    public string Theme { get; set; } = "Dark";
    public int ControlsAutoHideDelaySeconds { get; set; } = 3;
    public bool EnableAnimations { get; set; } = true;

    // Files & Directories
    public string ScreenshotDirectory { get; set; } = string.Empty;

    // Privacy
    public bool RecordPlaybackHistory { get; set; } = true;

    // Advanced
    public bool HardwareAcceleration { get; set; } = true;
    public string LogLevel { get; set; } = "Information";
}
