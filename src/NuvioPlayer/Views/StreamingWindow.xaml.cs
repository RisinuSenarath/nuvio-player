using System.Windows;
using System.Windows.Input;
using NuvioPlayer.ViewModels;

namespace NuvioPlayer.Views;

public partial class StreamingWindow : Window
{
    private readonly StreamingViewModel _viewModel;

    public StreamingWindow(StreamingViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.CloseRequested += () => Close();
        _viewModel.PlayRequested += (url, title) =>
        {
            // Close streaming modal so the user returns seamlessly to the main player
            Close();
        };
        _viewModel.PlayWebStreamRequested += (id, type, s, ep, title, prov) =>
        {
            Close();
        };

        Loaded += async (s, e) =>
        {
            await _viewModel.InitializeAsync();
        };
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                DragMove();
            }
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            if (MaximizeIconPath != null)
            {
                MaximizeIconPath.Data = (System.Windows.Media.Geometry)FindResource("IconWindowMaximize");
            }
        }
        else
        {
            WindowState = WindowState.Maximized;
            if (MaximizeIconPath != null)
            {
                MaximizeIconPath.Data = (System.Windows.Media.Geometry)FindResource("IconWindowRestore");
            }
        }
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.SearchCommand.Execute(null);
        }
    }
}
