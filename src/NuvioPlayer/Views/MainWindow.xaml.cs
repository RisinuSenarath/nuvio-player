using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;
    private bool _isDraggingSeek = false;

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

        // Video, Seek, and Playlist interactions
        SetupVideoInteractions();
        PlaylistListBox.MouseDoubleClick += (s, e) =>
        {
            if (PlaylistListBox.SelectedItem is PlaylistItem item)
            {
                _viewModel.Playlist.PlayItem(item);
            }
        };
    }

    private void SetupVideoInteractions()
    {
        VideoHostGrid.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                _viewModel.ToggleFullscreenCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.ClickCount == 1 && e.OriginalSource == VideoHostGrid || e.OriginalSource == PlayerVideoView)
            {
                _viewModel.TogglePlayPauseCommand.Execute(null);
            }
        };

        VideoHostGrid.MouseWheel += (s, e) =>
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Ctrl + Wheel = Seek
                int seconds = e.Delta > 0 ? 10 : -10;
                _viewModel.SeekRelativeCommand.Execute(seconds);
            }
            else
            {
                // Wheel = Volume
                int delta = e.Delta > 0 ? 5 : -5;
                _viewModel.ChangeVolumeCommand.Execute(delta);
            }
            e.Handled = true;
        };

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
                // Click on track seeking
                double percent = e.GetPosition(SeekSlider).X / SeekSlider.ActualWidth;
                double target = percent * SeekSlider.Maximum;
                _viewModel.EndUserSeek(target);
            }
        };
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        _viewModel.AreControlsVisible = true;
        Cursor = Cursors.Arrow;

        _inactivityTimer.Stop();
        if (_viewModel.IsPlaying)
        {
            _inactivityTimer.Start();
        }
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
                else
                {
                    OnRequestOpenFile();
                    e.Handled = true;
                }
                break;

            case Key.P:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    _viewModel.TogglePlaylistCommand.Execute(null);
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                if (files.Length == 1 && Directory.Exists(files[0]))
                {
                    // Dragging a folder: Scan for supported media
                    var supportedMedia = MediaFileHelper.ScanDirectoryForMedia(files[0], recursive: true);
                    if (supportedMedia.Count > 0)
                    {
                        await _viewModel.HandleMediaPathsAsync(supportedMedia);
                    }
                    else
                    {
                        _viewModel.ShowOsd("No supported media files found in folder.");
                    }
                }
                else if (files.Length == 1 && MediaFileHelper.IsSupportedSubtitleFile(files[0]))
                {
                    _viewModel.MediaPlayerService.AddSubtitleFile(files[0]);
                    _viewModel.ShowOsd($"Loaded Subtitle: {System.IO.Path.GetFileName(files[0])}");
                }
                else
                {
                    var mediaFiles = files.Where(MediaFileHelper.IsSupportedMediaFile).ToList();
                    if (mediaFiles.Count > 0)
                    {
                        await _viewModel.HandleMediaPathsAsync(mediaFiles);
                    }
                    else
                    {
                        _viewModel.ShowOsd("Unsupported file format.");
                    }
                }
            }
        }
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        RestoreWindowState();
    }

    private void RestoreWindowState()
    {
        var settings = _settingsService.Settings;

        if (settings.RememberWindowSize && settings.WindowWidth >= MinWidth && settings.WindowHeight >= MinHeight)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        if (settings.RememberWindowPosition)
        {
            double left = settings.WindowLeft;
            double top = settings.WindowTop;

            double screenLeft = SystemParameters.VirtualScreenLeft;
            double screenTop = SystemParameters.VirtualScreenTop;
            double screenWidth = SystemParameters.VirtualScreenWidth;
            double screenHeight = SystemParameters.VirtualScreenHeight;

            if (left >= screenLeft && left + Width <= screenLeft + screenWidth &&
                top >= screenTop && top + Height <= screenTop + screenHeight)
            {
                Left = left;
                Top = top;
            }
        }

        if (settings.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        UpdateMaximizeRestoreVisuals();
    }

    private void SaveWindowState()
    {
        var settings = _settingsService.Settings;

        if (WindowState == WindowState.Maximized)
        {
            settings.IsMaximized = true;
        }
        else if (WindowState == WindowState.Normal)
        {
            settings.IsMaximized = false;
            settings.WindowWidth = ActualWidth;
            settings.WindowHeight = ActualHeight;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        SaveWindowState();
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeRestoreVisuals();
    }

    private void UpdateMaximizeRestoreVisuals()
    {
        if (MaximizeIconPath == null || MaximizeButton == null) return;

        if (WindowState == WindowState.Maximized)
        {
            MaximizeIconPath.Data = (Geometry)FindResource("IconWindowRestore");
            MaximizeButton.ToolTip = "Restore Down";
        }
        else
        {
            MaximizeIconPath.Data = (Geometry)FindResource("IconWindowMaximize");
            MaximizeButton.ToolTip = "Maximize";
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
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
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;

            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;

            TitleBarRow.Height = new GridLength(0);
            FullscreenIconPath.Data = (Geometry)FindResource("IconExitFullscreen");
        }
        else
        {
            TitleBarRow.Height = new GridLength(38);
            ResizeMode = _previousResizeMode;
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;

            FullscreenIconPath.Data = (Geometry)FindResource("IconFullscreen");
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
