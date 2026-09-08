# Nuvio Player

<div align="center">

**A Modern, Lightweight, and Powerful Windows Desktop Media Player**

[![.NET](https://img.shields.io/badge/.NET-8.0_WPF-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Media Engine](https://img.shields.io/badge/Engine-LibVLC_3.x-FF8800?logo=vlc-media-player&logoColor=white)](https://code.videolan.org/videolan/LibVLCSharp)
[![Database](https://img.shields.io/badge/Database-SQLite_%2F_EF_Core-003B57?logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

</div>

---

## Overview

**Nuvio Player** is a modern Windows media player built from the ground up to provide broad format compatibility, smooth hardware-accelerated playback, fast startup times, and an uncluttered, distraction-free desktop user experience.

Unlike legacy players laden with 90s-style skeuomorphic chrome or bloated menus, Nuvio puts the video front and center. A sleek, auto-hiding floating HUD, custom borderless titlebar, and refined dark theme keep controls within effortless reach while preserving a cinematic viewing experience.

---

## Key Features

- **Broad Codec & Container Support:** Powered by a bundled, self-contained LibVLC runtime—no external codecs or VLC player installation required.
  - *Video Containers:* MP4, MKV, AVI, MOV, WebM, MPEG, MPG, TS, M2TS, FLV, WMV.
  - *Video Codecs:* H.264 (AVC), H.265 (HEVC), AV1, VP8, VP9, MPEG-2.
  - *Audio Formats:* MP3, FLAC, WAV, OGG, M4A, AAC, WMA, Opus.
- **Hardware-Accelerated Rendering:** High-framerate Direct3D11 video output with dynamic aspect ratio management (Default, 16:9, 4:3, 21:9, 1:1, Fit, Fill).
- **Modern Windows Desktop UI:**
  - Custom borderless titlebar with responsive window controls and screen-boundary awareness.
  - Floating playback HUD with sleek seekbar, volume slider, time badges, and auto-hide inactivity timer.
  - Subtle on-screen display (OSD) feedback for volume, seeking, audio tracks, and subtitles.
- **Advanced Subtitles & Audio:**
  - Automatic detection of matching external `.srt`, `.ass`, `.ssa`, and `.vtt` files.
  - Multi-track audio and subtitle stream switching.
  - Real-time subtitle delay (`G` / `H`) and audio delay (`J` / `K`) adjustment with OSD readout.
  - Drag-and-drop subtitle loading directly onto the video surface.
- **Playlist & Queue Management:**
  - Collapsible right sidebar (`Ctrl+P`) with drag-and-drop file reordering.
  - M3U and M3U8 playlist import and export.
  - Smart playback modes: Shuffle and Repeat (None, All, One).
  - Multi-file and recursive folder drag-and-drop loading.
- **Playback History & Smart Resume:**
  - SQLite database tracking recent playback, progress percentage, and timestamps.
  - Intelligent resume prompt ("*Resume from 42:17?*") when reopening previously viewed media.
- **Media Bookmarks:** Save named, timestamped bookmarks with instant jump links and persistent SQLite storage.
- **Frame-Accurate Screenshots:** Capture high-resolution video frames (`F12`) directly to your Windows Pictures directory.
- **Media Information Inspector:** Deep codec, resolution, bitrate, frame rate, and stream diagnostic dialog.
- **Windows Integration:**
  - Single-instance process management via system named mutex and local Named Pipe IPC.
  - Command-line file and folder arguments parsing (`NuvioPlayer.exe "C:\Movies\film.mkv"`).
  - Per-user Windows file association manager (`HKCU\Software\Classes`) with Explorer shell refresh.
- **Comprehensive Settings:** 9-category preferences panel (General, Playback, Interface, Subtitles, Audio, Keyboard, Files, Privacy, Advanced).

---

## Keyboard & Mouse Shortcuts

| Key / Gesture | Action |
|---|---|
| **Space** | Play / Pause |
| **Left Arrow** | Seek backward (5 seconds) |
| **Right Arrow** | Seek forward (5 seconds) |
| **Shift + Left Arrow** | Seek backward (30 seconds) |
| **Shift + Right Arrow** | Seek forward (30 seconds) |
| **Up Arrow** / **Mouse Wheel** | Increase volume |
| **Down Arrow** / **Mouse Wheel** | Decrease volume |
| **Ctrl + Mouse Wheel** | Seek backward / forward |
| **M** | Mute / Unmute audio |
| **F** / **Double Click** | Toggle borderless fullscreen |
| **Esc** | Exit fullscreen |
| **N** | Next item in playlist |
| **P** | Previous item in playlist |
| **S** | Cycle subtitle tracks |
| **G** / **H** | Subtitle delay down / up (50ms) |
| **J** / **K** | Audio delay down / up (50ms) |
| **F12** | Capture screenshot |
| **I** | Media Information dialog |
| **Ctrl + O** / **O** | Open File dialog |
| **Ctrl + Shift + O** | Open Folder dialog |
| **Ctrl + P** | Toggle Playlist & Bookmarks sidebar |

---

## Technology Stack

- **Framework:** .NET 8.0 Windows Desktop (WPF)
- **Language:** C# 12.0 (Nullable Reference Types enabled)
- **Media Engine:** LibVLCSharp 3.9.4 with bundled `VideoLAN.LibVLC.Windows` (win-x64)
- **MVVM Architecture:** CommunityToolkit.Mvvm
- **Database / ORM:** SQLite with Entity Framework Core 8
- **Dependency Injection:** `Microsoft.Extensions.DependencyInjection`
- **Logging:** `Microsoft.Extensions.Logging` with persistent file logging
- **Testing:** xUnit (74 automated unit & integration tests)
- **Packaging:** Inno Setup 6 & PowerShell automation

---

## System Requirements

- **Operating System:** Windows 10 (version 1809+) or Windows 11, 64-bit.
- **Processor:** 64-bit Intel / AMD dual-core processor or better.
- **Memory:** 2 GB RAM minimum (4 GB+ recommended for 4K video).
- **Graphics:** DirectX 11 capable graphics hardware.
- **VLC Installation:** **Not required.** All required LibVLC libraries are bundled.

---

## Development & Build Instructions

### Prerequisites
1. [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Windows 10 or 11 (64-bit)
3. Visual Studio 2022 / JetBrains Rider / VS Code with C# Dev Kit (optional)
4. [Inno Setup 6](https://jrsoftware.org/isdl.php) (optional, for installer creation)

### Building from Source

```powershell
# Clone or navigate to the repository
cd "C:\Users\RISINU\Documents\P\Nuvio Player"

# Restore dependencies and build in Debug configuration
dotnet build NuvioPlayer.slnx

# Run all automated tests
dotnet test NuvioPlayer.slnx

# Run the player
dotnet run --project src/NuvioPlayer/NuvioPlayer.csproj
```

### Release & Distribution Build

To compile a production Release build and standalone distribution:

```powershell
# Using the automated build script
pwsh -File scripts/publish.ps1

# Or via dotnet CLI
dotnet publish src/NuvioPlayer/NuvioPlayer.csproj -c Release -r win-x64 -o publish/win-x64 --self-contained false
```

The published package in `publish\win-x64` is standalone and includes:
- `NuvioPlayer.exe`
- `e_sqlite3.dll`
- `libvlc\win-x64\libvlc.dll`, `libvlccore.dll`, and `plugins/`

### Creating the Installer

With Inno Setup 6 installed:

```powershell
iscc.exe installer/NuvioPlayer.iss
```
The resulting installer is saved to `installer/output/NuvioPlayer-0.1.0-Setup.exe`.

---

## Architecture & Design Documentation

For in-depth technical documentation, refer to the `docs/` directory:
- [ARCHITECTURE.md](docs/ARCHITECTURE.md) — Service layer, MVVM, IPC, and media engine design.
- [QA_CHECKLIST.md](docs/QA_CHECKLIST.md) — 40-point manual verification test matrix.
- [IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md) — Development milestones and verification logs.
- [ROADMAP.md](docs/ROADMAP.md) — V1, V1.1, and V2 feature trajectory.

---

## License

Copyright © 2026 Nuvio. Distributed under the [MIT License](LICENSE).
