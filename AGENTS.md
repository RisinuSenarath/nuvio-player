# Nuvio Player — Agent Guidelines & Policies

## Release & Publishing Policy
- **On-Demand GitHub Release Only:** Do NOT compile the Inno Setup installer or publish releases to GitHub unless the user explicitly asks to publish an installer / release.
- **Development Mode by Default:** During standard workflow, focus purely on feature development, bug fixes, local testing (`dotnet test NuvioPlayer.slnx`), and git branch commits.
- When the user explicitly requests to publish a release, use `scripts/publish.ps1` and `scripts/create_github_release.ps1`.

## Core Architectural Constraints
- **Hardware Acceleration:** Keep `AllowsTransparency="False"` on `MainWindow.xaml` to preserve zero-copy Direct3D11 rendering via LibVLC.
- **Automated Testing:** All unit and integration tests must pass (`dotnet test NuvioPlayer.slnx`) before committing changes.
- **Git Branch Strategy:** Feature development takes place on `develop` and is merged into `main`. Both branches should be kept in sync with `origin`.
- **Keyboard & Mouse Mapping:**
  - `Enter` and `F`: Toggle Fullscreen / Windowed.
  - Video Surface Single Click: No action (preserves focus and drag).
  - Video Surface Double Click: Toggle Play / Pause.
  - Dedicated Hardware Multimedia Volume Keys: Must pass through to Windows OS master volume control.
