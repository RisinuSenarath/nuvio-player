using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NuvioPlayer.Helpers;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;
using NuvioPlayer.Services;
using NuvioPlayer.ViewModels;
using Xunit;

namespace NuvioPlayer.Tests;

public class WindowsIntegrationTests
{
    [Theory]
    [InlineData("video.mp4", true)]
    [InlineData("movie.mkv", true)]
    [InlineData("clip.avi", true)]
    [InlineData("stream.ts", true)]
    [InlineData("film.webm", true)]
    [InlineData("audio.mp3", true)]
    [InlineData("music.flac", true)]
    [InlineData("document.pdf", false)]
    [InlineData("executable.exe", false)]
    [InlineData("image.png", false)]
    public void MediaFileHelper_IsSupportedMediaFile_IdentifiesCorrectly(string fileName, bool expected)
    {
        bool actual = MediaFileHelper.IsSupportedMediaFile(fileName);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("subs.srt", true)]
    [InlineData("subs.ass", true)]
    [InlineData("subs.ssa", true)]
    [InlineData("subs.vtt", true)]
    [InlineData("subs.sub", true)]
    [InlineData("subs.txt", false)]
    public void MediaFileHelper_IsSupportedSubtitleFile_IdentifiesCorrectly(string fileName, bool expected)
    {
        bool actual = MediaFileHelper.IsSupportedSubtitleFile(fileName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MediaFileHelper_GetOpenFileDialogFilter_ReturnsValidFilter()
    {
        string filter = MediaFileHelper.GetOpenFileDialogFilter();
        Assert.Contains("*.mp4", filter);
        Assert.Contains("*.mkv", filter);
        Assert.Contains("Video Files", filter);
        Assert.Contains("Audio Files", filter);
        Assert.Contains("All Files (*.*)|*.*", filter);
    }

    [Fact]
    public void CommandLineService_ParseMediaPaths_FiltersValidFilesAndIgnoresSwitches()
    {
        var service = new CommandLineService(NullLogger<CommandLineService>.Instance);

        string tempDir = Path.Combine(Path.GetTempPath(), "Nuvio_CLITest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string validVideo = Path.Combine(tempDir, "video1.mp4");
            string validAudio = Path.Combine(tempDir, "audio1.mp3");
            string invalidDoc = Path.Combine(tempDir, "readme.txt");

            File.WriteAllText(validVideo, "dummy");
            File.WriteAllText(validAudio, "dummy");
            File.WriteAllText(invalidDoc, "dummy");

            var args = new[]
            {
                "--fullscreen",
                "-v",
                validVideo,
                invalidDoc,
                validAudio,
                "\"" + validVideo + "\"" // Duplicate with quotes
            };

            var parsed = service.ParseMediaPaths(args);

            Assert.Equal(2, parsed.Count);
            Assert.Equal(validVideo, parsed[0]);
            Assert.Equal(validAudio, parsed[1]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CommandLineService_ParseMediaPaths_ExpandsFolder()
    {
        var service = new CommandLineService(NullLogger<CommandLineService>.Instance);

        string tempDir = Path.Combine(Path.GetTempPath(), "Nuvio_CLIFolderTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string vidA = Path.Combine(tempDir, "episode1.mkv");
            string vidB = Path.Combine(tempDir, "episode2.mp4");
            string notes = Path.Combine(tempDir, "info.nfo");

            File.WriteAllText(vidA, "dummy");
            File.WriteAllText(vidB, "dummy");
            File.WriteAllText(notes, "dummy");

            var args = new[] { tempDir };
            var parsed = service.ParseMediaPaths(args);

            Assert.Equal(2, parsed.Count);
            Assert.Contains(vidA, parsed);
            Assert.Contains(vidB, parsed);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MainViewModel_HandleMediaPathsAsync_SingleFilePlaysDirectly()
    {
        var mockMedia = new MockMediaPlayerService();
        var settingsService = new SettingsService(NullLogger<SettingsService>.Instance);
        var playlistService = new PlaylistService(NullLogger<PlaylistService>.Instance);
        var playlistVm = new PlaylistViewModel(playlistService, mockMedia);
        var fakeDb = new FakeDatabaseService();

        var vm = new MainViewModel(
            NullLogger<MainViewModel>.Instance,
            settingsService,
            mockMedia,
            playlistService,
            playlistVm,
            fakeDb);

        string testFile = @"C:\Media\movie.mp4";
        await vm.HandleMediaPathsAsync(new[] { testFile });

        Assert.Equal(testFile, mockMedia.CurrentMediaPath);
        Assert.True(mockMedia.IsPlaying);
    }

    [Fact]
    public async Task MainViewModel_HandleMediaPathsAsync_MultipleFilesQueuesPlaylist()
    {
        var mockMedia = new MockMediaPlayerService();
        var settingsService = new SettingsService(NullLogger<SettingsService>.Instance);
        var playlistService = new PlaylistService(NullLogger<PlaylistService>.Instance);
        var playlistVm = new PlaylistViewModel(playlistService, mockMedia);
        var fakeDb = new FakeDatabaseService();

        var vm = new MainViewModel(
            NullLogger<MainViewModel>.Instance,
            settingsService,
            mockMedia,
            playlistService,
            playlistVm,
            fakeDb);

        string file1 = @"C:\Media\ep1.mp4";
        string file2 = @"C:\Media\ep2.mp4";
        string file3 = @"C:\Media\ep3.mp4";

        await vm.HandleMediaPathsAsync(new[] { file1, file2, file3 });

        Assert.Equal(3, vm.Playlist.Items.Count);
        Assert.Equal(file1, mockMedia.CurrentMediaPath);
        Assert.True(vm.IsPlaylistOpen);
    }
}
