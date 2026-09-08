# Nuvio Player — Manual QA Checklist & Verification Matrix

This document defines the 40 standard test scenarios required to validate Nuvio Player on target platforms (Windows 10 & Windows 11, 64-bit).

---

## Playback & Format Compatibility

| # | Test Scenario | Steps to Execute | Expected Result | Status |
|---|---|---|---|---|
| 1 | **Open MP4** | Click Open File (or Ctrl+O) and select an `.mp4` container. | Video initializes promptly, starts playing with audio, seekbar reflects total duration. | PASS |
| 2 | **Open MKV** | Drag and drop or open an `.mkv` container. | Video opens cleanly, demuxes video/audio/subtitles, smooth playback. | PASS |
| 3 | **Open H.265/HEVC video** | Open an HEVC/H.265 encoded video (1080p/4K). | Hardware-accelerated decoding via bundled LibVLC, 0 dropped frames, audio sync. | PASS |
| 4 | **Open H.264 video** | Open an AVC/H.264 encoded file. | Immediate playback, zero artifacting, correct color representation. | PASS |
| 5 | **Open AV1 video** | Open an AV1-encoded video container. | Decodes smoothly with bundled VLC AV1 decoder library (dav1d/bundled libvlc). | PASS |

---

## Core Playback Controls

| # | Test Scenario | Steps to Execute | Expected Result | Status |
|---|---|---|---|---|
| 6 | **Play / Pause** | Press Spacebar, click Play/Pause button, or click video area. | Video toggles between playing and paused; icon updates; subtle OSD notification displays. | PASS |
| 7 | **Stop** | Click Stop button (or right click > Stop). | Video stops completely, resets timeline to 00:00, clears surface cleanly. | PASS |
| 8 | **Seeking** | 1. Drag seekbar thumb.<br>2. Click seekbar track.<br>3. Left/Right Arrow keys (5s).<br>4. Shift+Left/Right keys (30s).<br>5. Ctrl + Mouse wheel. | Playhead jumps accurately to target timestamp without audio stuttering or desync. | PASS |
| 9 | **Volume** | 1. Adjust volume slider.<br>2. Up/Down Arrow keys.<br>3. Mouse wheel over video surface. | Volume level changes smoothly between 0% and 100%; OSD pill displays percentage. | PASS |
| 10 | **Mute** | Press `M` or click Mute icon button. | Audio mutes instantly, volume icon shows muted state, pressing M restores previous volume. | PASS |

---

## Windowing, Viewports & Gestures

| # | Test Scenario | Steps to Execute | Expected Result | Status |
|---|---|---|---|---|
| 11 | **Fullscreen** | Press `F`, press `Alt+Enter`, or double-click video area. | Window expands to borderless fullscreen covering taskbar; titlebar collapses; cursor hides upon inactivity. Pressing `Esc` or `F` restores window cleanly. | PASS |
| 12 | **Keyboard Shortcuts** | Test Space, Arrows, Shift+Arrows, M, F, Esc, N, P, S, G, H, J, K, F12, I, Ctrl+O, Ctrl+Shift+O, Ctrl+P. | Every action executes immediately without focus loss. | PASS |
| 13 | **Mouse Controls** | 1. Double click: Fullscreen toggle.<br>2. Mouse wheel: Volume change.<br>3. Ctrl+Wheel: Seek.<br>4. Right-click: Context menu. | Responsive gestures with zero lag or accidental triggers. | PASS |

---

## Subtitles & Audio

| # | Test Scenario | Steps to Execute | Expected Result | Status |
|---|---|---|---|---|
| 14 | **Subtitle Loading** | 1. Video with embedded softsubs.<br>2. Auto-load sibling `movie.srt`.<br>3. Drag external `.srt`/`.ass` file onto video.<br>4. Open Subtitle File dialog. | Subtitle track appears immediately and renders accurately with configured font size and color. | PASS |
| 15 | **Subtitle Delay** | Press `G` (backward 50ms) or `H` (forward 50ms). | Subtitle delay changes in increments of ±50ms; OSD displays current offset (e.g. `+100ms`). | PASS |
| 16 | **Audio Track Selection** | Right-click video > Audio Track > choose from available audio streams. | Stream switches instantaneously without player restart or audio popping. | PASS |
| 17 | **Audio Delay** | Press `J` (-50ms) or `K` (+50ms). | Audio delays or advances; OSD feedback confirms offset. | PASS |
| 18 | **Video Track Selection** | Right-click video > Video Track > choose alternate video angle/stream. | Switches to selected video track seamlessly. | PASS |

---

## Playlist Management

| # | Test Scenario | Steps to Execute | Expected Result | Status |
|---|---|---|---|---|
| 19 | **Playlist Operations** | Open sidebar (`Ctrl+P`), click Add (`+`), remove item (`X`), clear playlist. | Items populate with title and duration; double clicking an item starts playback immediately. | PASS |
| 20 | **Shuffle** | Click Shuffle icon button. | Playback advances through playlist in random non-repeating order. | PASS |
| 21 | **Repeat Modes** | Click Repeat button to cycle: Repeat Off → Repeat All → Repeat One. | Correct end-of-track behavior: loops current item, loops entire playlist, or stops at end. | PASS |

---

## User Data, State & Advanced Features

| # | Test Scenario | Steps to Execute | Expected Result | Status |
|---|---|---|---|---|
| 22 | **Resume Playback** | Play video to 40%, close app, reopen the same video. | Small prompt asks "Resume from 04:12?". Clicking Resume jumps to position; clicking Start Over starts at 00:00. | PASS |
| 23 | **Screenshot** | Press `F12`, click camera HUD icon, or choose Screenshot in context menu. | Current frame captured as high-resolution PNG, saved to Pictures/configured directory, toast confirmation shown. | PASS |
| 24 | **Bookmarks** | Click "+ Add" in Bookmarks tab or press bookmark shortcut. | Bookmark created with timestamp; clicking item jumps to exact frame; persists across app restarts. | PASS |
| 25 | **Drag and Drop** | 1. Drag single video: Plays immediately.<br>2. Drag multiple videos: Queues to playlist.<br>3. Drag folder: Recursively scans and queues. | Flawless drag-and-drop parsing without UI freezing. | PASS |
| 26 | **Recent History** | Open multiple media files; return to Home screen. | History list displays recent media with progress bar, relative timestamp, and remove options. | PASS |
| 27 | **Settings Persistence** | Change seek step, theme, volume, or screenshot directory; restart app. | All preferences survive restart via SQLite and `%APPDATA%\NuvioPlayer\settings.json`. | PASS |
| 28 | **Window Persistence** | Resize and reposition window; close and relaunch. | Window restores to exact geometry and state (clamped to virtual monitor bounds). | PASS |
| 29 | **Multiple Monitors** | Move player to secondary monitor, enter fullscreen, exit fullscreen. | Maintains monitor affinity; fullscreen stays on current display without jumping. | PASS |

---

## Robustness, Edge Cases & Error Handling

| # | Test Scenario | Steps to Execute | Expected Result | Status |
|---|---|---|---|---|
| 30 | **Unsupported File** | Drag `.txt` or `.exe` into player. | User-friendly OSD notification ("Unsupported file format"); application remains fully responsive. | PASS |
| 31 | **Missing File** | Delete a file from disk that is listed in Recent Media or Playlist, then click it. | Displays graceful message ("File no longer exists"); does not crash. | PASS |
| 32 | **Corrupt Media** | Attempt to open a truncated or corrupted media file. | Error caught by LibVLC service; OSD informs user; player resets to stopped state cleanly. | PASS |
| 33 | **Application Restart** | Rapidly open, close, and restart Nuvio Player. | Clean shutdown of native threads and SQLite handles; no orphan processes in Task Manager. | PASS |
| 34 | **Command-line Opening** | Run `NuvioPlayer.exe "C:\Videos\clip.mp4"`. | Launches directly into video playback. | PASS |
| 35 | **File Associations** | Double-click an associated `.mp4` file in Windows Explorer. | Windows invokes Nuvio Player and begins playback. | PASS |
| 36 | **Single-Instance Behavior** | With Nuvio Player already running, double-click another video in Explorer. | Existing window activates and brings to front; opens or queues the new video without spawning a duplicate instance. | PASS |

---

## Deployment, Packaging & Release Verification

| # | Test Scenario | Steps to Execute | Expected Result | Status |
|---|---|---|---|---|
| 37 | **Installer Deployment** | Run `NuvioPlayer-0.1.0-Setup.exe` on clean Windows installation. | Installs without admin prompts; creates Start Menu & Desktop shortcuts; registers chosen file associations. | PASS |
| 38 | **Uninstaller** | Run Uninstaller from Settings > Apps. | Cleanly removes application binaries and shortcuts; preserves user data unless explicitly requested. | PASS |
| 39 | **Clean-Machine Launch** | Launch published `NuvioPlayer.exe` on a computer without VLC installed. | Starts immediately using bundled `libvlc\win-x64` native runtime; video playback functions 100%. | PASS |
| 40 | **Resource & Memory Behavior** | Repeatedly open 20 different 4K and 1080p media files in sequence. | Memory usage stays stable; previous media objects and native surfaces are disposed; zero memory leaks. | PASS |
