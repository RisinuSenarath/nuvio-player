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

    private Point _topBarMouseDownPos;
    private double _topBarPercentX = 0.5;
    private bool _isTopBarPressed = false;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    // DWM Window Attributes (Windows 11)
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
    private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetClassLong")]
    private static extern int SetClassLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(int crColor);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const int GCLP_HBRBACKGROUND = -10;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const uint GA_ROOT = 2;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;

    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;

    private IntPtr _hBackgroundBrush = IntPtr.Zero;

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

        LocationChanged += (s, e) =>
        {
            if (WindowState == WindowState.Normal)
            {
                _previousWindowRect = new Rect(Left, Top, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
            }
        };

        SizeChanged += (s, e) =>
        {
            if (WindowState == WindowState.Normal)
            {
                _previousWindowRect = new Rect(Left, Top, ActualWidth, ActualHeight);
            }
        };

        ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;

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

        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);

            SetClassBackgroundBrush(helper.Handle);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_ERASEBKGND:
                handled = true;
                return (IntPtr)1;

            case WM_GETMINMAXINFO:
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                if (_viewModel.IsFullscreen)
                {
                    // Fullscreen covers the entire physical monitor
                    mmi.ptMaxPosition.X = 0;
                    mmi.ptMaxPosition.Y = 0;
                    mmi.ptMaxSize.X = Math.Abs(monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left);
                    mmi.ptMaxSize.Y = Math.Abs(monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top);
                    mmi.ptMaxTrackSize.X = mmi.ptMaxSize.X;
                    mmi.ptMaxTrackSize.Y = mmi.ptMaxSize.Y;
                }
                else
                {
                    // Maximized window respects the work area (does NOT block or cover the Windows taskbar)
                    mmi.ptMaxPosition.X = monitorInfo.rcWork.Left - monitorInfo.rcMonitor.Left;
                    mmi.ptMaxPosition.Y = monitorInfo.rcWork.Top - monitorInfo.rcMonitor.Top;
                    mmi.ptMaxSize.X = Math.Abs(monitorInfo.rcWork.Right - monitorInfo.rcWork.Left);
                    mmi.ptMaxSize.Y = Math.Abs(monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top);
                    mmi.ptMaxTrackSize.X = mmi.ptMaxSize.X;
                    mmi.ptMaxTrackSize.Y = mmi.ptMaxSize.Y;
                }
            }
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private static IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
        {
            return SetClassLongPtr64(hWnd, nIndex, dwNewLong);
        }
        return new IntPtr(SetClassLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    private void SetClassBackgroundBrush(IntPtr hwnd)
    {
        try
        {
            // #0B0D11 in COLORREF (0x00BBGGRR) -> R=0x0B, G=0x0D, B=0x11 -> 0x00110D0B
            _hBackgroundBrush = CreateSolidBrush(0x00110D0B);
            if (_hBackgroundBrush != IntPtr.Zero)
            {
                SetClassLongPtr(hwnd, GCLP_HBRBACKGROUND, _hBackgroundBrush);
            }
        }
        catch
        {
            // Fallback silently if class brush modification is restricted
        }
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

                // DWM frame/caption color matching BrushBackground (#0B0D11)
                int captionColor = 0x00110D0B;
                DwmSetWindowAttribute(helper.Handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
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
                _isTopBarPressed = false;
                ToggleMaximizeRestore();
            }
            else
            {
                if (WindowState == WindowState.Maximized)
                {
                    _isTopBarPressed = true;
                    _topBarMouseDownPos = e.GetPosition(this);
                    _topBarPercentX = _topBarMouseDownPos.X / Math.Max(1.0, ActualWidth);
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
    }

    private void OnTopBarMouseMove(object sender, MouseEventArgs e)
    {
        if (_isTopBarPressed && e.LeftButton == MouseButtonState.Pressed && WindowState == WindowState.Maximized && !_viewModel.IsFullscreen)
        {
            Point currentPos = e.GetPosition(this);
            Vector diff = currentPos - _topBarMouseDownPos;
            if (Math.Abs(diff.X) > 6 || Math.Abs(diff.Y) > 6)
            {
                _isTopBarPressed = false;
                RestoreFromMaximizedDrag(_topBarPercentX);
                e.Handled = true;
            }
        }
        else if (e.LeftButton != MouseButtonState.Pressed)
        {
            _isTopBarPressed = false;
        }
    }

    private void OnTopBarMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _isTopBarPressed = false;
        }
    }

    private void RestoreFromMaximizedDrag(double percentX)
    {
        double restoredWidth = _previousWindowRect.Width > 200 ? _previousWindowRect.Width : _settingsService.Settings.WindowWidth;
        if (restoredWidth < MinWidth || restoredWidth > SystemParameters.VirtualScreenWidth)
        {
            restoredWidth = 1200;
        }

        double restoredHeight = _previousWindowRect.Height > 200 ? _previousWindowRect.Height : _settingsService.Settings.WindowHeight;
        if (restoredHeight < MinHeight || restoredHeight > SystemParameters.VirtualScreenHeight)
        {
            restoredHeight = 720;
        }

        percentX = Math.Clamp(percentX, 0.05, 0.95);

        if (GetCursorPos(out POINT cursorPt))
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            double cursorDipX = cursorPt.X / (dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0);
            double cursorDipY = cursorPt.Y / (dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0);

            WindowState = WindowState.Normal;
            Width = restoredWidth;
            Height = restoredHeight;
            Left = cursorDipX - (restoredWidth * percentX);
            Top = cursorDipY - 16;
            SetWindowCornerPreference(true);

            try
            {
                DragMove();
            }
            catch
            {
                // Ignored if drag operation is cancelled or interrupted
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

    private static bool IsInteractiveControl(DependencyObject? element)
    {
        while (element != null && element is not MainWindow)
        {
            if (element is ButtonBase || element is Slider || element is Thumb ||
                element is TextBoxBase || element is ListBoxItem || element is ListBox ||
                element is ContextMenu || element is MenuItem || element is ProgressBar)
                return true;

            if (element is FrameworkElement fe && 
                (fe.Name == "ControlsHud" || fe.Name == "TopOverlayBar" || fe.Name == "ResumePromptBanner"))
                return true;

            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void OnVideoOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        this.Focus();
        OnUserActivityDetected();

        if (IsInteractiveControl(e.OriginalSource as DependencyObject))
        {
            _isMouseDownOnVideo = false;
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            _isMouseDownOnVideo = true;
            _mouseDownPos = e.GetPosition(this);

            if (e.ClickCount == 2)
            {
                _isMouseDownOnVideo = false;
                _viewModel.TogglePlayPauseCommand.Execute(null);
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
                if (WindowState == WindowState.Maximized)
                {
                    RestoreFromMaximizedDrag(_mouseDownPos.X / Math.Max(1.0, ActualWidth));
                }
                else
                {
                    try
                    {
                        DragMove();
                    }
                    catch
                    {
                        // Ignored
                    }
                }
                e.Handled = true;
            }
        }
    }

    private void OnVideoOverlayMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isMouseDownOnVideo = false;
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

        if (_isTopBarPressed && e.LeftButton == MouseButtonState.Pressed && WindowState == WindowState.Maximized && !_viewModel.IsFullscreen)
        {
            Point currentPos = e.GetPosition(this);
            Vector diff = currentPos - _topBarMouseDownPos;
            if (Math.Abs(diff.X) > 6 || Math.Abs(diff.Y) > 6)
            {
                _isTopBarPressed = false;
                RestoreFromMaximizedDrag(_topBarPercentX);
                e.Handled = true;
            }
        }
        else if (e.LeftButton != MouseButtonState.Pressed)
        {
            _isTopBarPressed = false;
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

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (handled) return;

        if (msg.message == WM_KEYDOWN || msg.message == WM_SYSKEYDOWN)
        {
            if (ComponentDispatcher.IsThreadModal)
            {
                return;
            }

            if (Keyboard.FocusedElement is TextBoxBase)
            {
                return;
            }

            var helper = new WindowInteropHelper(this);
            IntPtr mainHwnd = helper.Handle;
            if (mainHwnd != IntPtr.Zero)
            {
                IntPtr rootHwnd = GetAncestor(msg.hwnd, GA_ROOT);
                var fgWindow = GetVlcForegroundWindow();
                IntPtr fgHwnd = fgWindow != null ? new WindowInteropHelper(fgWindow).Handle : IntPtr.Zero;

                if (rootHwnd != mainHwnd && rootHwnd != fgHwnd && msg.hwnd != mainHwnd && msg.hwnd != fgHwnd)
                {
                    return;
                }
            }

            Key key = KeyInterop.KeyFromVirtualKey((int)msg.wParam);
            if (HandlePlayerKey(key))
            {
                handled = true;
            }
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase)
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (HandlePlayerKey(key))
        {
            e.Handled = true;
        }
    }

    private bool HandlePlayerKey(Key key)
    {
        bool handled = false;

        switch (key)
        {
            case Key.Space:
            case Key.MediaPlayPause:
                _viewModel.TogglePlayPauseCommand.Execute(null);
                handled = true;
                break;

            case Key.MediaStop:
                _viewModel.StopCommand.Execute(null);
                handled = true;
                break;

            case Key.Left:
                int seekBack = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                    ? -_settingsService.Settings.LongSeekSeconds
                    : -_settingsService.Settings.ShortSeekSeconds;
                _viewModel.SeekRelativeCommand.Execute(seekBack);
                handled = true;
                break;

            case Key.Right:
                int seekFwd = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                    ? _settingsService.Settings.LongSeekSeconds
                    : _settingsService.Settings.ShortSeekSeconds;
                _viewModel.SeekRelativeCommand.Execute(seekFwd);
                handled = true;
                break;

            case Key.Up:
                _viewModel.ChangeVolumeCommand.Execute(5);
                handled = true;
                break;

            case Key.Down:
                _viewModel.ChangeVolumeCommand.Execute(-5);
                handled = true;
                break;

            case Key.M:
                _viewModel.ToggleMuteCommand.Execute(null);
                handled = true;
                break;

            case Key.F:
            case Key.Return:
                _viewModel.ToggleFullscreenCommand.Execute(null);
                handled = true;
                break;

            case Key.Escape:
                if (_viewModel.IsFullscreen)
                {
                    _viewModel.IsFullscreen = false;
                    handled = true;
                }
                break;

            case Key.S:
                _viewModel.ToggleSubtitlesCommand.Execute(null);
                handled = true;
                break;

            case Key.G:
                _viewModel.AdjustSubtitleDelayCommand.Execute(-50);
                handled = true;
                break;

            case Key.H:
                _viewModel.AdjustSubtitleDelayCommand.Execute(50);
                handled = true;
                break;

            case Key.J:
                _viewModel.AdjustAudioDelayCommand.Execute(-50);
                handled = true;
                break;

            case Key.K:
                _viewModel.AdjustAudioDelayCommand.Execute(50);
                handled = true;
                break;

            case Key.I:
                _viewModel.OpenMediaInfoCommand.Execute(null);
                handled = true;
                break;

            case Key.F12:
                _viewModel.TakeScreenshotCommand.Execute(null);
                handled = true;
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
                    handled = true;
                }
                break;

            case Key.P:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    _viewModel.TogglePlaylistCommand.Execute(null);
                    handled = true;
                }
                else
                {
                    _viewModel.PreviousCommand.Execute(null);
                    handled = true;
                }
                break;

            case Key.MediaPreviousTrack:
                _viewModel.PreviousCommand.Execute(null);
                handled = true;
                break;

            case Key.N:
            case Key.MediaNextTrack:
                _viewModel.NextCommand.Execute(null);
                handled = true;
                break;
        }

        if (handled)
        {
            OnUserActivityDetected();
        }

        return handled;
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
        ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
        SaveWindowBounds();
        _inactivityTimer.Stop();
        _cursorPollTimer.Stop();

        if (_hBackgroundBrush != IntPtr.Zero)
        {
            DeleteObject(_hBackgroundBrush);
            _hBackgroundBrush = IntPtr.Zero;
        }
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
            SetWindowCornerPreference(true);
        }
        else
        {
            WindowState = WindowState.Maximized;
            SetWindowCornerPreference(false);
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        bool isMaximized = WindowState == WindowState.Maximized;
        if (!_viewModel.IsFullscreen)
        {
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
        if (fgWindow != null)
        {
            fgWindow.PreviewKeyDown -= OnForegroundWindowPreviewKeyDown;
            fgWindow.PreviewKeyDown += OnForegroundWindowPreviewKeyDown;
        }

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

    private void OnForegroundWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase)
        {
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (HandlePlayerKey(key))
        {
            e.Handled = true;
        }
    }

    private void UpdatePlayPauseVisuals()
    {
        if (PlayPauseIconPath == null) return;
        PlayPauseIconPath.Data = _viewModel.IsPlaying
            ? (Geometry)FindResource("IconPause")
            : (Geometry)FindResource("IconPlay");

        if (PlayPauseViewbox != null)
        {
            PlayPauseViewbox.Margin = new Thickness(0);
        }
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
