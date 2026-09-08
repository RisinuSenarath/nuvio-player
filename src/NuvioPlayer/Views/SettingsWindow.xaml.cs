using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NuvioPlayer.ViewModels;

namespace NuvioPlayer.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose += () => Close();
        _viewModel.ShowMessage += msg =>
        {
            MessageBox.Show(this, msg, "Nuvio Player", MessageBoxButton.OK, MessageBoxImage.Information);
        };
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;

        // Hide all panels
        PanelGeneral.Visibility = Visibility.Collapsed;
        PanelPlayback.Visibility = Visibility.Collapsed;
        PanelInterface.Visibility = Visibility.Collapsed;
        PanelSubtitles.Visibility = Visibility.Collapsed;
        PanelAudio.Visibility = Visibility.Collapsed;
        PanelKeyboard.Visibility = Visibility.Collapsed;
        PanelFiles.Visibility = Visibility.Collapsed;
        PanelPrivacy.Visibility = Visibility.Collapsed;
        PanelAdvanced.Visibility = Visibility.Collapsed;

        // Show selected panel
        switch (tag)
        {
            case "0": PanelGeneral.Visibility = Visibility.Visible; break;
            case "1": PanelPlayback.Visibility = Visibility.Visible; break;
            case "2": PanelInterface.Visibility = Visibility.Visible; break;
            case "3": PanelSubtitles.Visibility = Visibility.Visible; break;
            case "4": PanelAudio.Visibility = Visibility.Visible; break;
            case "5": PanelKeyboard.Visibility = Visibility.Visible; break;
            case "6": PanelFiles.Visibility = Visibility.Visible; break;
            case "7": PanelPrivacy.Visibility = Visibility.Visible; break;
            case "8": PanelAdvanced.Visibility = Visibility.Visible; break;
        }
    }
}
