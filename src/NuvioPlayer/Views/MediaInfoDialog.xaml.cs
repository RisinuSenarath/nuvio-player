using System.Text;
using System.Windows;
using System.Windows.Input;
using NuvioPlayer.Models;

namespace NuvioPlayer.Views;

public partial class MediaInfoDialog : Window
{
    private readonly MediaDetails _details;

    public MediaInfoDialog(MediaDetails details)
    {
        InitializeComponent();
        _details = details;
        DataContext = _details;
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"File Name: {_details.FileName}");
        sb.AppendLine($"Full Path: {_details.FilePath}");
        sb.AppendLine($"Container: {_details.Container}");
        sb.AppendLine($"Duration: {_details.FormattedDuration}");
        sb.AppendLine($"File Size: {_details.FormattedFileSize}");
        sb.AppendLine($"Video Codec: {_details.VideoCodec}");
        sb.AppendLine($"Resolution: {_details.ResolutionText}");
        sb.AppendLine($"Frame Rate: {_details.FrameRateText}");
        sb.AppendLine($"Audio Codec: {_details.AudioCodec}");
        sb.AppendLine($"Audio Channels: {_details.AudioChannelsText}");
        sb.AppendLine($"Sample Rate: {_details.AudioSampleRateText}");
        sb.AppendLine($"Audio Tracks: {_details.AudioTracksSummary}");
        sb.AppendLine($"Subtitles: {_details.SubtitlesSummary}");

        Clipboard.SetText(sb.ToString());
        MessageBox.Show(this, "Media information copied to clipboard.", "Nuvio Player", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
