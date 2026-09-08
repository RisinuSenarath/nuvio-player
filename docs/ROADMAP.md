# Nuvio Player — Product Roadmap

This document outlines the product evolution and milestone roadmap for Nuvio Player across releases.

---

## Milestone 1: Version 1.0 (Current Baseline)
*Focus: Rock-solid local playback, modern Windows UI, high format compatibility, and seamless Windows integration.*

- [x] High-performance video playback powered by bundled LibVLC 3.x runtime.
- [x] Full format support: MP4, MKV, AVI, MOV, WebM, TS, M2TS, FLV, WMV, MP3, FLAC, WAV.
- [x] Codecs: H.264, H.265/HEVC, VP8, VP9, AV1, MPEG-2.
- [x] Custom borderless modern titlebar with window controls and screen-boundary awareness.
- [x] Floating HUD controls with auto-hide timer and cursor management during fullscreen.
- [x] Comprehensive keyboard shortcuts & mouse controls (wheel volume, ctrl-wheel seek, double click fullscreen).
- [x] Multi-track audio and subtitle selection with auto-detection of sibling `.srt`/`.ass`/`.vtt` files.
- [x] Subtitle & Audio delay fine-tuning (±50ms steps) with on-screen display (OSD) feedback.
- [x] Playlist sidebar with drag-and-drop, M3U/M3U8 import/export, shuffle, and repeat modes.
- [x] SQLite EF Core persistence for history, playlists, bookmarks, and settings.
- [x] Smart resume playback prompt with position thresholds.
- [x] Frame-accurate screenshot capture to user-defined Pictures directory.
- [x] Media information dialog displaying codecs, bitrates, resolutions, and sample rates.
- [x] Single-instance enforcement with Named Pipe IPC for forwarding command-line files.
- [x] Per-user Windows file association manager (HKCU) with Explorer shell refresh.
- [x] Automated test suite (74 tests) and Inno Setup installer script.

---

## Milestone 2: Version 1.1 (Enhancement Release)
*Focus: Extended desktop convenience, audio processing, and layout options.*

- [ ] **Picture-in-Picture (PiP) Mode:** Floating, always-on-top borderless mini window with compact controls.
- [ ] **Compact Mini-Player View:** Audio-first compact mode with playback controls and track details.
- [ ] **Graphic Equalizer:** 10-band audio equalizer with genre presets (Rock, Pop, Classical, Flat, Bass Boost).
- [ ] **Audio Visualizer:** Subtle frequency spectrum visualization during audio-only playback.
- [ ] **Extended Color Themes:** OLED Pure Black theme and Windows Light theme with runtime dynamic switching.
- [ ] **Customizable Hotkeys Editor:** UI interface in Settings allowing users to rebind all player shortcuts.
- [ ] **Hardware Acceleration Profiles:** Granular selection of DXVA2, D3D11VA, or software fallback decoding.

---

## Milestone 3: Version 2.0 (Next-Generation Media Experience)
*Focus: Network streaming, library organization, casting, and extensibility.*

- [ ] **Network Media Streaming:** Direct playback of RTSP, RTMP, HLS (`.m3u8`), and HTTP/HTTPS video streams.
- [ ] **Rich Media Library:** Grid view with poster art, collections, tag filtering, and metadata scrapers.
- [ ] **Online Metadata Fetching:** Automatic retrieval of movie/show posters, plot synopses, and cast info.
- [ ] **Google Cast / DLNA Support:** Cast local media to Smart TVs and Chromecast devices on the local network.
- [ ] **Extensibility & Plugin Architecture:** Managed plugin API allowing third-party developers to contribute custom decoders, subtitle downloaders, and theme extensions.
- [ ] **Cloud Playlist Synchronization:** Optional encrypted synchronization of bookmarks and playlists.
