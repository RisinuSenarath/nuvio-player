using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NuvioPlayer.Helpers;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;
using NuvioPlayer.ViewModels;

namespace NuvioPlayer.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _inactivityTimer;
    private readonly DispatcherTimer _cursorPollTimer;
    private POINT _lastGlobalCursorPos;

    private Rect _previousWindowRect = new Rect(100, 100, 1200, 720);
    private WindowState _previousWindowState = WindowState.Normal;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;

    private Point _mouseDownPos;
    private bool _isMouseDownOnVideo = false;
    private bool _isDraggingSeek = false;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    // DWM Window Attributes (Windows 11)
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;

    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;
    private const double WindowCornerRadius = 12;

    public MainWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        // Auto-hide controls timer
        _inactivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_settingsService.Settings.ControlsAutoHideDelaySeconds)
        };
        _inactivityTimer.Tick += OnInactivityTick;

        // Cursor polling timer for detecting mouse activity even over unmanaged video surface
        _cursorPollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _cursorPollTimer.Tick += OnCursorPollTick;
        _cursorPollTimer.Start();

        PlayerVideoView.Loaded += (s, e) => UpdateVideoOverlayVisibility();

        // Window commands
        _viewModel.RequestMinimize += () => WindowState = WindowState.Minimized;
        _viewModel.RequestMaximize += ToggleMaximizeRestore;
        _viewModel.RequestClose += () => Close();
        _viewModel.RequestOpenFile += OnRequestOpenFile;
        _viewModel.RequestOpenFolder += OnRequestOpenFolder;
        _viewModel.RequestOpenMediaInfo += OnRequestOpenMediaInfo;
        _viewModel.RequestOpenSettings += OnRequestOpenSettings;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Window events
        StateChanged += OnWindowStateChanged;
        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
        MouseMove += OnWindowMouseMove;
        PreviewKeyDown += OnWindowPreviewKeyDown;

        // Drag & Drop
        Drop += OnWindowDrop;
        DragOver += OnWindowDragOver;

        // Seek slider & playlist setup
        SetupControlsInteractions();

        PlaylistListBox.MouseDoubleClick += (s, e) =>
        {
            if (PlaylistListBox.SelectedItem is PlaylistItem item)
            {
                _viewModel.Playlist.PlayItem(item);
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        SetWindowCornerPreference(true);
    }

    private void SetWindowCornerPreference(bool round)
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                // Dark mode title/frame for seamless rendering
                int darkMode = 1;
                DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                // Rounded corner preference (Windows 11 hardware-anti-aliased native curve)
                int preference = round ? DWMWCP_ROUND : DWMWCP_DONOTROUND;
                DwmSetWindowAttribute(helper.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));

                // Subtle dark border color matching BrushSurfaceBorderSubtle (#1D212B)
                int borderColor = 0x002B211D;
                DwmSetWindowAttribute(helper.Handle, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
            }
        }
        catch
        {
            // Fallback silently if DWM attributes are not supported on this OS
        }
    }

    private void SetupControlsInteractions()
    {
        SeekSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((s, e) =>
        {
            _isDraggingSeek = true;
            _viewModel.BeginUserSeek();
        }));

        SeekSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((s, e) =>
        {
            _isDraggingSeek = false;
            _viewModel.EndUserSeek(SeekSlider.Value);
        }));

        SeekSlider.PreviewMouseDown += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDraggingSeek)
            {
                double percent = e.GetPosition(SeekSlider).X / SeekSlider.ActualWidth;
                double target = percent * SeekSlider.Maximum;
                _viewModel.EndUserSeek(target);
            }
        };
    }

    private void OnTopBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
            }
            else
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // Ignored if window drag is interrupted
                }
            }
        }
    }

    private void OnCursorPollTick(object? sender, EventArgs e)
    {
        if (!_viewModel.HasMedia)
        {
            return;
        }

        if (GetCursorPos(out POINT pt))
        {
            if (pt.X != _lastGlobalCursorPos.X || pt.Y != _lastGlobalCursorPos.Y)
            {
                _lastGlobalCursorPos = pt;

                try
                {
                    Point localPoint = PointFromScreen(new Point(pt.X, pt.Y));
                    if (localPoint.X >= 0 && localPoint.X <= ActualWidth &&
                        localPoint.Y >= 0 && localPoint.Y <= ActualHeight)
                    {
                        OnUserActivityDetected();
                    }
                }
                catch
                {
                    // Ignore during window state or minimize transitions
                }
            }
        }
    }

    private void OnUserActivityDetected()
    {
        if (!_viewModel.HasMedia)
        {
            return;
        }

        if (!_viewModel.AreControlsVisible)
        {
            _viewModel.AreControlsVisible = true;
        }

        if (Cursor != Cursors.Arrow)
        {
            Cursor = Cursors.Arrow;
        }

        _inactivityTimer.Stop();
        if (_viewModel.IsPlaying)
        {
            _inactivityTimer.Start();
        }
    }

    private void OnVideoOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        OnUserActivityDetected();

        if (e.ChangedButton == MouseButton.Left)
        {
            _isMouseDownOnVideo = true;
            _mouseDownPos = e.GetPosition(this);

            if (e.ClickCount == 2)
            {
                _isMouseDownOnVideo = false;
                _viewModel.ToggleFullscreenCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnVideoOverlayMouseMove(object sender, MouseEventArgs e)
    {
        OnUserActivityDetected();

        if (_isMouseDownOnVideo && e.LeftButton == MouseButtonState.Pressed && !_viewModel.IsFullscreen)
        {
            Point currentPos = e.GetPosition(this);
            Vector diff = currentPos - _mouseDownPos;
            if (Math.Abs(diff.X) > 8 || Math.Abs(diff.Y) > 8)
            {
                _isMouseDownOnVideo = false;
                try
                {
                    DragMove();
                }
                catch
                {
                    // Ignored
                }
                e.Handled = true;
            }
        }
    }

    private void OnVideoOverlayMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMouseDownOnVideo && e.ChangedButton == MouseButton.Left)
        {
            _isMouseDownOnVideo = false;
            // Check if clicked directly on video canvas or overlay grid (not on interactive buttons/sliders)
            if (e.OriginalSource == VideoOverlayGrid || e.OriginalSource is Grid || e.OriginalSource == PlayerVideoView)
            {
                _viewModel.TogglePlayPauseCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnVideoOverlayMouseWheel(object sender, MouseWheelEventArgs e)
    {
        OnUserActivityDetected();

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            int seconds = e.Delta > 0 ? 10 : -10;
            _viewModel.SeekRelativeCommand.Execute(seconds);
        }
        else
        {
            int delta = e.Delta > 0 ? 5 : -5;
            _viewModel.ChangeVolumeCommand.Execute(delta);
        }
        e.Handled = true;
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        OnUserActivityDetected();
    }

    private void OnInactivityTick(object? sender, EventArgs e)
    {
        _inactivityTimer.Stop();
        if (_viewModel.IsPlaying && !_isDraggingSeek)
        {
            _viewModel.AreControlsVisible = false;
            if (_viewModel.IsFullscreen)
            {
                Cursor = Cursors.None;
            }
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                _viewModel.TogglePlayPauseCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Left:
                int seekBack = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                    ? -_settingsService.Settings.LongSeekSeconds
                    : -_settingsService.Settings.ShortSeekSeconds;
                _viewModel.SeekRelativeCommand.Execute(seekBack);
                e.Handled = true;
                break;

            case Key.Right:
                int seekFwd = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                    ? _settingsService.Settings.LongSeekSeconds
                    : _settingsService.Settings.ShortSeekSeconds;
                _viewModel.SeekRelativeCommand.Execute(seekFwd);
                e.Handled = true;
                break;

            case Key.Up:
                _viewModel.ChangeVolumeCommand.Execute(5);
                e.Handled = true;
                break;

            case Key.Down:
                _viewModel.ChangeVolumeCommand.Execute(-5);
                e.Handled = true;
                break;

            case Key.M:
                _viewModel.ToggleMuteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F:
                _viewModel.ToggleFullscreenCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                if (_viewModel.IsFullscreen)
                {
                    _viewModel.IsFullscreen = false;
                    e.Handled = true;
                }
                break;

            case Key.S:
                _viewModel.ToggleSubtitlesCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.G:
                _viewModel.AdjustSubtitleDelayCommand.Execute(-50);
                e.Handled = true;
                break;

            case Key.H:
                _viewModel.AdjustSubtitleDelayCommand.Execute(50);
                e.Handled = true;
                break;

            case Key.J:
                _viewModel.AdjustAudioDelayCommand.Execute(-50);
                e.Handled = true;
                break;

            case Key.K:
                _viewModel.AdjustAudioDelayCommand.Execute(50);
                e.Handled = true;
                break;

            case Key.I:
                _viewModel.OpenMediaInfoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F12:
                _viewModel.TakeScreenshotCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.O:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        OnRequestOpenFolder();
                    }
                    else
                    {
                        OnRequestOpenFile();
                    }
                    e.Handled = true;
                }
                break;

            case Key.P:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    _viewModel.TogglePlaylistCommand.Execute(null);
                    e.Handled = true;
                }
                else
                {
                    _viewModel.PreviousCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.N:
                _viewModel.NextCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var validMedia = new List<string>();
                foreach (var path in files)
                {
                    if (Directory.Exists(path))
                    {
                        validMedia.AddRange(MediaFileHelper.ScanDirectoryForMedia(path, recursive: true));
                    }
                    else if (MediaFileHelper.IsSupportedSubtitleFile(path))
                    {
                        _viewModel.LoadSubtitleFileCommand.Execute(path);
                    }
                    else if (MediaFileHelper.IsSupportedMediaFile(path))
                    {
                        validMedia.Add(path);
                    }
                }

                if (validMedia.Count > 0)
                {
                    _ = _viewModel.HandleMediaPathsAsync(validMedia);
                }
            }
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowBounds();
        UpdateVideoOverlayVisibility();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        SaveWindowBounds();
        _inactivityTimer.Stop();
        _cursorPollTimer.Stop();
    }

    private void ApplyWindowBounds()
    {
        var s = _settingsService.Settings;
        if (s.RememberWindowSize && s.WindowWidth > 200 && s.WindowHeight > 200)
        {
            Width = s.WindowWidth;
            Height = s.WindowHeight;
        }

        if (s.RememberWindowPosition && s.WindowLeft >= 0 && s.WindowTop >= 0)
        {
            double screenWidth = SystemParameters.VirtualScreenWidth;
            double screenHeight = SystemParameters.VirtualScreenHeight;

            if (s.WindowLeft + 100 < screenWidth && s.WindowTop + 100 < screenHeight)
            {
                Left = s.WindowLeft;
                Top = s.WindowTop;
            }
        }

        if (s.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveWindowBounds()
    {
        var s = _settingsService.Settings;
        s.IsMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = (int)ActualWidth;
            s.WindowHeight = (int)ActualHeight;
            s.WindowLeft = (int)Left;
            s.WindowTop = (int)Top;
        }
        _ = _settingsService.SaveAsync();
    }

    private void ToggleMaximizeRestore()
    {
        if (_viewModel.IsFullscreen)
        {
            _viewModel.ToggleFullscreenCommand.Execute(null);
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            RootBorder.CornerRadius = new CornerRadius(WindowCornerRadius);
            RootBorder.BorderThickness = new Thickness(1);
            SetWindowCornerPreference(true);
        }
        else
        {
            WindowState = WindowState.Maximized;
            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.BorderThickness = new Thickness(0);
            SetWindowCornerPreference(false);
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        bool isMaximized = WindowState == WindowState.Maximized;
        if (!_viewModel.IsFullscreen)
        {
            RootBorder.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(WindowCornerRadius);
            RootBorder.BorderThickness = isMaximized ? new Thickness(0) : new Thickness(1);
            SetWindowCornerPreference(!isMaximized);
        }

        var iconData = isMaximized
            ? (Geometry)FindResource("IconWindowRestore")
            : (Geometry)FindResource("IconWindowMaximize");

        if (MaximizeIconPath != null)
        {
            MaximizeIconPath.Data = iconData;
        }
        if (HomeMaximizeIconPath != null)
        {
            HomeMaximizeIconPath.Data = iconData;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.HasMedia):
                UpdateVideoOverlayVisibility();
                break;

            case nameof(MainViewModel.IsFullscreen):
                ApplyFullscreen(_viewModel.IsFullscreen);
                break;

            case nameof(MainViewModel.IsPlaying):
                UpdatePlayPauseVisuals();
                if (_viewModel.IsPlaying)
                {
                    _inactivityTimer.Start();
                }
                else
                {
                    _inactivityTimer.Stop();
                    _viewModel.AreControlsVisible = true;
                    Cursor = Cursors.Arrow;
                }
                break;

            case nameof(MainViewModel.IsMuted):
            case nameof(MainViewModel.Volume):
                UpdateVolumeVisuals();
                break;
        }
    }

    private Window? GetVlcForegroundWindow()
    {
        try
        {
            var prop = typeof(LibVLCSharp.WPF.VideoView).GetProperty("ForegroundWindow",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return prop?.GetValue(PlayerVideoView) as Window;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateVideoOverlayVisibility()
    {
        var fgWindow = GetVlcForegroundWindow();
        if (_viewModel.HasMedia)
        {
            VideoOverlayGrid.Visibility = Visibility.Visible;
            if (fgWindow != null)
            {
                fgWindow.Visibility = Visibility.Visible;
            }
        }
        else
        {
            VideoOverlayGrid.Visibility = Visibility.Collapsed;
            if (fgWindow != null)
            {
                fgWindow.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void UpdatePlayPauseVisuals()
    {
        if (PlayPauseIconPath == null) return;
        PlayPauseIconPath.Data = _viewModel.IsPlaying
            ? (Geometry)FindResource("IconPause")
            : (Geometry)FindResource("IconPlay");
    }

    private void UpdateVolumeVisuals()
    {
        if (VolumeIconPath == null) return;

        if (_viewModel.IsMuted || _viewModel.Volume == 0)
        {
            VolumeIconPath.Data = (Geometry)FindResource("IconVolumeMute");
        }
        else if (_viewModel.Volume < 40)
        {
            VolumeIconPath.Data = (Geometry)FindResource("IconVolumeLow");
        }
        else
        {
            VolumeIconPath.Data = (Geometry)FindResource("IconVolumeHigh");
        }
    }

    private void ApplyFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            _previousWindowState = WindowState;
            _previousResizeMode = ResizeMode;
            _previousWindowRect = new Rect(Left, Top, Width, Height);

            // Remove WindowChrome in fullscreen so window stretches edge-to-edge without taskbar margins
            WindowChrome.SetWindowChrome(this, null);

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.BorderThickness = new Thickness(0);
            SetWindowCornerPreference(false);

            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;

            Topmost = true;

            if (FullscreenIconPath != null)
            {
                FullscreenIconPath.Data = (Geometry)FindResource("IconExitFullscreen");
            }
        }
        else
        {
            Topmost = false;

            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;

            Left = _previousWindowRect.Left;
            Top = _previousWindowRect.Top;
            Width = _previousWindowRect.Width;
            Height = _previousWindowRect.Height;

            RootBorder.CornerRadius = new CornerRadius(WindowCornerRadius);
            RootBorder.BorderThickness = new Thickness(1);
            SetWindowCornerPreference(true);

            // Restore WindowChrome
            WindowChrome.SetWindowChrome(this, WindowChromeElement);

            if (FullscreenIconPath != null)
            {
                FullscreenIconPath.Data = (Geometry)FindResource("IconFullscreen");
            }

            Cursor = Cursors.Arrow;
        }
    }

    private async void OnRequestOpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Media File — Nuvio Player",
            Filter = MediaFileHelper.GetOpenFileDialogFilter(),
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true && dialog.FileNames.Length > 0)
        {
            await _viewModel.HandleMediaPathsAsync(dialog.FileNames);
        }
    }

    private async void OnRequestOpenFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Open Media Folder — Nuvio Player",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            var mediaFiles = MediaFileHelper.ScanDirectoryForMedia(dialog.FolderName, recursive: true);
            if (mediaFiles.Count > 0)
            {
                await _viewModel.HandleMediaPathsAsync(mediaFiles);
            }
            else
            {
                _viewModel.ShowOsd("No supported media files found in selected folder.");
            }
        }
    }

    private void OnRequestOpenMediaInfo()
    {
        var details = _viewModel.MediaPlayerService.GetCurrentMediaDetails();
        if (details != null)
        {
            var dialog = new MediaInfoDialog(details) { Owner = this };
            dialog.ShowDialog();
        }
        else
        {
            _viewModel.ShowOsd("No media currently playing");
        }
    }

    private void OnRequestOpenSettings()
    {
        var settingsVm = App.Services.GetRequiredService<SettingsViewModel>();
        var window = new SettingsWindow(settingsVm) { Owner = this };
        window.ShowDialog();
    }

    private void OnVideoContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        PopulateAudioTracksMenu();
        PopulateSubtitleTracksMenu();
        PopulateVideoTracksMenu();
    }

    private void PopulateAudioTracksMenu()
    {
        AudioTrackMenuItem.Items.Clear();

        if (_viewModel.AudioTracks.Count == 0)
        {
            var emptyItem = new MenuItem { Header = "No Audio Tracks", IsEnabled = false };
            AudioTrackMenuItem.Items.Add(emptyItem);
            return;
        }

        foreach (var track in _viewModel.AudioTracks)
        {
            var trackItem = new MenuItem
            {
                Header = track.Name,
                IsCheckable = true,
                IsChecked = track.Id == _viewModel.SelectedAudioTrackId
            };
            int trackId = track.Id;
            trackItem.Click += (s, ev) => _viewModel.SelectAudioTrackCommand.Execute(trackId);
            AudioTrackMenuItem.Items.Add(trackItem);
        }

        AudioTrackMenuItem.Items.Add(new Separator());

        var disableItem = new MenuItem
        {
            Header = "Disable Audio",
            IsCheckable = true,
            IsChecked = _viewModel.SelectedAudioTrackId == -1
        };
        disableItem.Click += (s, ev) => _viewModel.SelectAudioTrackCommand.Execute(-1);
        AudioTrackMenuItem.Items.Add(disableItem);
    }

    private void PopulateSubtitleTracksMenu()
    {
        SubtitleTrackMenuItem.Items.Clear();

        var disableItem = new MenuItem
        {
            Header = "Disable Subtitles",
            IsCheckable = true,
            IsChecked = _viewModel.SelectedSubtitleTrackId == -1
        };
        disableItem.Click += (s, ev) => _viewModel.SelectSubtitleTrackCommand.Execute(-1);
        SubtitleTrackMenuItem.Items.Add(disableItem);

        var loadItem = new MenuItem
        {
            Header = "Load Subtitle File..."
        };
        loadItem.Click += (s, ev) => _viewModel.LoadSubtitleFileCommand.Execute(null);
        SubtitleTrackMenuItem.Items.Add(loadItem);

        if (_viewModel.SubtitleTracks.Count > 0)
        {
            SubtitleTrackMenuItem.Items.Add(new Separator());
            foreach (var track in _viewModel.SubtitleTracks)
            {
                var trackItem = new MenuItem
                {
                    Header = track.Name,
                    IsCheckable = true,
                    IsChecked = track.Id == _viewModel.SelectedSubtitleTrackId
                };
                int trackId = track.Id;
                trackItem.Click += (s, ev) => _viewModel.SelectSubtitleTrackCommand.Execute(trackId);
                SubtitleTrackMenuItem.Items.Add(trackItem);
            }
        }
    }

    private void PopulateVideoTracksMenu()
    {
        VideoTrackMenuItem.Items.Clear();

        if (_viewModel.VideoTracks.Count <= 1)
        {
            VideoTrackMenuItem.Visibility = Visibility.Collapsed;
            return;
        }

        VideoTrackMenuItem.Visibility = Visibility.Visible;
        foreach (var track in _viewModel.VideoTracks)
        {
            var trackItem = new MenuItem
            {
                Header = track.Name,
                IsCheckable = true,
                IsChecked = track.Id == _viewModel.SelectedVideoTrackId
            };
            int trackId = track.Id;
            trackItem.Click += (s, ev) => _viewModel.SelectVideoTrackCommand.Execute(trackId);
            VideoTrackMenuItem.Items.Add(trackItem);
        }
    }
}
