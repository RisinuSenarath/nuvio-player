using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NuvioPlayer.Database.Entities;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;
using NuvioPlayer.ViewModels;
using Xunit;

namespace NuvioPlayer.Tests;

internal class FakeSettingsService : ISettingsService
{
    public UserSettings Settings { get; set; } = new();
    public bool SaveCalled { get; private set; }
    public bool ResetCalled { get; private set; }

    public Task LoadAsync() => Task.CompletedTask;

    public Task SaveAsync()
    {
        SaveCalled = true;
        return Task.CompletedTask;
    }

    public void ResetToDefaults()
    {
        ResetCalled = true;
        Settings = new UserSettings();
    }

    public string GetAppDataPath() => Path.GetTempPath();
}

internal class FakeDatabaseService : IDatabaseService
{
    public bool ClearHistoryCalled { get; private set; }
    public bool ClearBookmarksCalled { get; private set; }
    public bool ClearPlaylistsCalled { get; private set; }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task RecordPlaybackAsync(string filePath, string displayName, long positionMs, long durationMs) => Task.CompletedTask;
    public Task<List<MediaHistoryEntity>> GetRecentMediaAsync(int count = 20) => Task.FromResult(new List<MediaHistoryEntity>());
    public Task<MediaHistoryEntity?> GetHistoryForFileAsync(string filePath) => Task.FromResult<MediaHistoryEntity?>(null);
    public Task UpdatePositionAsync(string filePath, long positionMs) => Task.CompletedTask;
    public Task RemoveHistoryItemAsync(int id) => Task.CompletedTask;

    public Task ClearHistoryAsync()
    {
        ClearHistoryCalled = true;
        return Task.CompletedTask;
    }

    public Task<BookmarkEntity> AddBookmarkAsync(string filePath, long positionMs, string label)
    {
        return Task.FromResult(new BookmarkEntity { FilePath = filePath, PositionMs = positionMs, Label = label });
    }

    public Task<List<BookmarkEntity>> GetBookmarksForFileAsync(string filePath) => Task.FromResult(new List<BookmarkEntity>());
    public Task RemoveBookmarkAsync(int id) => Task.CompletedTask;

    public Task ClearBookmarksAsync()
    {
        ClearBookmarksCalled = true;
        return Task.CompletedTask;
    }

    public Task<List<PlaylistEntity>> GetAllPlaylistsAsync() => Task.FromResult(new List<PlaylistEntity>());
    public Task<PlaylistEntity> SavePlaylistAsync(string name, IEnumerable<string> filePaths) => Task.FromResult(new PlaylistEntity { Name = name });
    public Task DeletePlaylistAsync(int id) => Task.CompletedTask;

    public Task ClearPlaylistsAsync()
    {
        ClearPlaylistsCalled = true;
        return Task.CompletedTask;
    }

    public Task SaveActivePlaylistAsync(IEnumerable<string> filePaths) => Task.CompletedTask;
    public Task<List<string>> LoadActivePlaylistAsync() => Task.FromResult(new List<string>());
}

public class SettingsViewModelTests
{
    private readonly FakeSettingsService _fakeSettings;
    private readonly FakeDatabaseService _fakeDatabase;

    public SettingsViewModelTests()
    {
        _fakeSettings = new FakeSettingsService
        {
            Settings = new UserSettings
            {
                ResumePlayback = true,
                RememberWindowSize = true,
                RememberWindowPosition = true,
                DefaultPlaybackSpeed = 1.25,
                ShortSeekSeconds = 10,
                LongSeekSeconds = 60,
                AutoPlayNext = true,
                AspectRatio = "16:9",
                Theme = "Dark",
                ControlsAutoHideDelaySeconds = 4,
                EnableAnimations = true,
                AutoLoadSubtitles = true,
                SubtitleFontSize = 26,
                SubtitleColor = "Yellow",
                Volume = 85,
                ScreenshotDirectory = @"C:\TestPictures",
                RecordPlaybackHistory = true,
                HardwareAcceleration = true,
                LogLevel = "Debug"
            }
        };

        _fakeDatabase = new FakeDatabaseService();
    }

    [Fact]
    public void Constructor_LoadsCurrentSettingsProperly()
    {
        var vm = new SettingsViewModel(
            NullLogger<SettingsViewModel>.Instance,
            _fakeSettings,
            _fakeDatabase);

        Assert.True(vm.ResumePlayback);
        Assert.Equal(1.25, vm.DefaultPlaybackSpeed);
        Assert.Equal(10, vm.ShortSeekSeconds);
        Assert.Equal(60, vm.LongSeekSeconds);
        Assert.Equal("16:9", vm.AspectRatio);
        Assert.Equal("Dark", vm.Theme);
        Assert.Equal(4, vm.ControlsAutoHideDelaySeconds);
        Assert.True(vm.EnableAnimations);
        Assert.True(vm.AutoLoadSubtitles);
        Assert.Equal(26, vm.SubtitleFontSize);
        Assert.Equal("Yellow", vm.SubtitleColor);
        Assert.Equal(85, vm.DefaultVolume);
        Assert.Equal(@"C:\TestPictures", vm.ScreenshotDirectory);
        Assert.True(vm.RecordPlaybackHistory);
        Assert.True(vm.HardwareAcceleration);
        Assert.Equal("Debug", vm.LogLevel);
        Assert.NotEmpty(vm.Shortcuts);
        Assert.NotEmpty(vm.SupportedExtensions);
        Assert.Equal(0, vm.SelectedSectionIndex);
    }

    [Fact]
    public async Task SaveCommand_UpdatesSettingsServiceAndSaves()
    {
        var vm = new SettingsViewModel(
            NullLogger<SettingsViewModel>.Instance,
            _fakeSettings,
            _fakeDatabase);

        bool closeRequested = false;
        vm.RequestClose += () => closeRequested = true;

        vm.DefaultPlaybackSpeed = 1.5;
        vm.ShortSeekSeconds = 15;
        vm.Theme = "Light";
        vm.ResumePlayback = false;

        await vm.SaveSettingsCommand.ExecuteAsync(null);

        Assert.True(_fakeSettings.SaveCalled);
        Assert.Equal(1.5, _fakeSettings.Settings.DefaultPlaybackSpeed);
        Assert.Equal(15, _fakeSettings.Settings.ShortSeekSeconds);
        Assert.Equal("Light", _fakeSettings.Settings.Theme);
        Assert.False(_fakeSettings.Settings.ResumePlayback);
        Assert.True(closeRequested);
    }

    [Fact]
    public void ResetDefaultsCommand_CallsServiceResetAndReloads()
    {
        var vm = new SettingsViewModel(
            NullLogger<SettingsViewModel>.Instance,
            _fakeSettings,
            _fakeDatabase)
        {
            ConfirmDialog = (msg, title) => true
        };

        vm.DefaultPlaybackSpeed = 2.0;
        vm.ResetSettingsCommand.Execute(null);

        Assert.True(_fakeSettings.ResetCalled);
        Assert.Equal(1.0, vm.DefaultPlaybackSpeed);
    }

    [Fact]
    public async Task ClearHistoryCommand_CallsDatabaseService()
    {
        var vm = new SettingsViewModel(
            NullLogger<SettingsViewModel>.Instance,
            _fakeSettings,
            _fakeDatabase)
        {
            ConfirmDialog = (msg, title) => true
        };

        string? message = null;
        vm.ShowMessage += msg => message = msg;

        await vm.ClearHistoryCommand.ExecuteAsync(null);

        Assert.True(_fakeDatabase.ClearHistoryCalled);
        Assert.NotNull(message);
        Assert.Contains("cleared", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearBookmarksCommand_CallsDatabaseService()
    {
        var vm = new SettingsViewModel(
            NullLogger<SettingsViewModel>.Instance,
            _fakeSettings,
            _fakeDatabase)
        {
            ConfirmDialog = (msg, title) => true
        };

        string? message = null;
        vm.ShowMessage += msg => message = msg;

        await vm.ClearBookmarksCommand.ExecuteAsync(null);

        Assert.True(_fakeDatabase.ClearBookmarksCalled);
        Assert.NotNull(message);
        Assert.Contains("cleared", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectedSectionIndex_SwitchesActiveSection()
    {
        var vm = new SettingsViewModel(
            NullLogger<SettingsViewModel>.Instance,
            _fakeSettings,
            _fakeDatabase);

        Assert.Equal(0, vm.SelectedSectionIndex);

        vm.SelectedSectionIndex = 4; // Audio
        Assert.Equal(4, vm.SelectedSectionIndex);

        vm.SelectedSectionIndex = 8; // Advanced
        Assert.Equal(8, vm.SelectedSectionIndex);
    }
}
