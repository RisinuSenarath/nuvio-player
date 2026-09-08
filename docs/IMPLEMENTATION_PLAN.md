# Nuvio Player — Comprehensive Implementation Plan

## Overview
Nuvio Player is a modern, lightweight, high-performance Windows desktop media player built with C#, .NET 8 (WPF), MVVM architecture, LibVLCSharp media engine (bundled with native `VideoLAN.LibVLC.Windows` runtime), SQLite & EF Core persistence, and Microsoft DI & Logging.

---

## Technical Stack & Architecture

- **Platform:** Windows 10 / Windows 11 (64-bit)
- **Framework:** .NET 8.0 Windows Desktop SDK (WPF)
- **Pattern:** MVVM (Model-View-ViewModel) using CommunityToolkit.Mvvm
- **Media Engine:** LibVLCSharp 3.9.4 + VideoLAN.LibVLC.Windows 3.0.23.1 (fully standalone, zero-dependency on external VLC)
- **Database:** SQLite with Entity Framework Core 8.0
- **Dependency Injection:** Microsoft.Extensions.DependencyInjection
- **Logging:** Microsoft.Extensions.Logging with persistent file logging
- **Testing:** xUnit (74 automated unit and integration tests)
- **Packaging & Installer:** Inno Setup 6 (.iss) + dotnet publish win-x64

---

## Directory Structure

```
NuvioPlayer/
├── src/
│   ├── NuvioPlayer/
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── Configuration/
│   │   ├── Converters/
│   │   ├── Database/
│   │   │   ├── Entities/
│   │   │   └── NuvioDbContext.cs
│   │   ├── Helpers/
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   ├── Resources/
│   │   │   ├── Brushes.xaml
│   │   │   ├── Colors.xaml
│   │   │   ├── Icons.xaml
│   │   │   └── Styles.xaml
│   │   ├── Services/
│   │   ├── ViewModels/
│   │   └── Views/
│   └── NuvioPlayer.Tests/
├── installer/
│   └── NuvioPlayer.iss
├── docs/
│   ├── ARCHITECTURE.md
│   ├── IMPLEMENTATION_PLAN.md
│   ├── QA_CHECKLIST.md
│   └── ROADMAP.md
├── scripts/
│   └── publish.ps1
├── LICENSE
└── README.md
```

---

## Implementation Phase Status

### Phase 0: Discovery & Planning [COMPLETED]
- [x] Inspect development environment (.NET 8 SDK, Windows Desktop SDK).
- [x] Create solution & projects (`NuvioPlayer`, `NuvioPlayer.Tests`).
- [x] Add core dependencies: `LibVLCSharp.WPF`, `VideoLAN.LibVLC.Windows`, `Microsoft.EntityFrameworkCore.Sqlite`, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`.
- [x] Verify LibVLC native packaging (`libvlc/win-x64/` dlls & plugins present in build output).
- [x] Create project structure and `docs/IMPLEMENTATION_PLAN.md`.

### Phase 1: Application Foundation [COMPLETED]
- [x] Setup DI container in `App.xaml.cs` (Host builder / ServiceProvider).
- [x] Configure Logging and Application Lifecycle (`FileLoggerProvider`).
- [x] Implement dark theme resource dictionaries (`Colors.xaml`, `Brushes.xaml`, `Styles.xaml`, `Icons.xaml`).
- [x] Implement Custom Modern Window chrome & title bar with minimize/maximize/close, draggable title bar, title display, and Nuvio vector icon.
- [x] Setup ViewModels and Views with DI injection (`MainWindow`, `MainViewModel`).
- [x] Verified build succeeds with 0 warnings.

### Phase 2: Media Engine Integration [COMPLETED]
- [x] Define `IMediaPlayerService` interface isolating LibVLC details.
- [x] Implement `VlcMediaPlayerService` wrapping `LibVLC` and `MediaPlayer`.
- [x] Integrate `VideoView` WPF control into player host view.
- [x] Handle media loading (files, streams), Play, Pause, Stop, Seeking, State changes.
- [x] Handle errors, media parsing, duration discovery, and clean resource disposal.

### Phase 3: Player UI & Controls [COMPLETED]
- [x] Implement Video display area with overlay HUD.
- [x] Modern Playback control bar: Play/Pause/Stop, Prev/Next, Seek slider with smooth tracking, current time / total duration display, volume slider & mute button, playback speed selector, fullscreen toggle.
- [x] Auto-hide controls mechanism with timer on mouse inactivity.
- [x] Keyboard shortcuts handler (Space, Left/Right seeking, Up/Down volume, Mute, F fullscreen, Esc, etc.).
- [x] Mouse controls (double click fullscreen, wheel volume, ctrl+wheel seek, right click context menu).
- [x] Video OSD (subtle on-screen notifications for volume, seek, speed, subtitles).

### Phase 4: Playlist Management [COMPLETED]
- [x] Implement `IPlaylistService` and `PlaylistViewModel`.
- [x] Collapsible playlist sidebar (`Ctrl+P`).
- [x] Add files/folder, remove, clear, drag & drop.
- [x] Reorder items, double click to play.
- [x] Next, Previous, Shuffle, Repeat None / Repeat All / Repeat One modes.
- [x] M3U and M3U8 import and export.

### Phase 5: Database & Persistence [COMPLETED]
- [x] Configure SQLite EF Core DbContext (`NuvioDbContext`).
- [x] Entities: `MediaHistoryEntity`, `PlaylistEntity`, `PlaylistItemEntity`, `BookmarkEntity`.
- [x] Automatic database initialization & migration in `%AppData%/NuvioPlayer/nuvio.db`.
- [x] Repositories / Services for History, Bookmarks, Playlists, and Settings.

### Phase 6: Playback Position & Resume [COMPLETED]
- [x] Automatic position tracking during playback and on app exit.
- [x] Detect previous playback position on opening media.
- [x] Non-intrusive Resume prompt overlay ("Resume from 42:17? [Resume] [Start Over]").
- [x] Smart thresholds (ignore if < 10s or within last 15s of video).

### Phase 7: Subtitles & Audio Track Selection [COMPLETED]
- [x] Subtitle track enumeration and selection.
- [x] External subtitle loading (.srt, .ass, .ssa, .vtt).
- [x] Automatic detection of subtitles matching media file name in same directory.
- [x] Drag & drop subtitle file onto video.
- [x] Subtitle delay adjustment (`G` / `H` keys) with OSD display.
- [x] Audio track enumeration, selection, and audio delay adjustment (`J` / `K` keys).
- [x] Video track selection (multi-angle / alternative video streams).

### Phase 8: Advanced Features & Dialogs [COMPLETED]
- [x] Screenshot capture: capture current frame via LibVLC, save to configurable pictures folder, toast notification.
- [x] Bookmarks: add bookmark at timestamp with label, list bookmarks, jump to bookmark, delete bookmark.
- [x] Media Information dialog: container, video codec, resolution, framerate, bitrate, audio codec, channels, sample rate.
- [x] Aspect ratio selection (Default, 16:9, 4:3, 1:1, 21:9, Fit, Fill).
- [x] Playback speed selection (0.25x to 2.0x).
- [x] Home Screen (clean, modern landing page when no media is playing, showing drop target, open buttons, and recent media list).

### Phase 9: Settings System [COMPLETED]
- [x] `ISettingsService` and complete `SettingsWindow`.
- [x] Categorized settings: General, Playback, Interface, Subtitles, Audio, Keyboard, Files, Privacy, Advanced.
- [x] Reset to defaults, validation, and persistent file storage.

### Phase 10: Windows Integration [COMPLETED]
- [x] Single-instance application support via Named Mutex and local Named Pipe IPC.
- [x] Command-line argument parsing (launch with single video, multiple videos, or folders).
- [x] Per-user file associations registration helper (`HKCU\Software\Classes`).
- [x] Windows Explorer "Open with" and shell notification via `SHChangeNotify`.

### Phase 11: UI/UX Polish [COMPLETED]
- [x] Cohesive modern dark styling, typography, spacing, and subtle transitions.
- [x] Empty states for Playlist, Bookmarks, and Recent Media.
- [x] Clean resource dictionary aliasing (`BrushBorder`, `BrushTextTertiary`).
- [x] Fullscreen transitions, cursor hiding, and multi-monitor screen boundary clamping.

### Phase 12: Automated Testing & Verification [COMPLETED]
- [x] 74 unit/integration tests passing with 0 errors and 0 warnings.
- [x] Verified clean Debug and Release builds.
- [x] Verified published distribution in `publish/win-x64` with bundled native LibVLC runtime.

### Phase 13: Packaging & Release [COMPLETED]
- [x] Inno Setup installer script (`installer/NuvioPlayer.iss`).
- [x] Automated PowerShell publish and packaging script (`scripts/publish.ps1`).
- [x] Standalone win-x64 release build verified.
- [x] Complete documentation: `README.md`, `LICENSE`, `docs/ARCHITECTURE.md`, `docs/QA_CHECKLIST.md`, `docs/ROADMAP.md`.
