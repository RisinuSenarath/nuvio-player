using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using NuvioPlayer.Models;
using NuvioPlayer.Services;
using Xunit;

namespace NuvioPlayer.Tests;

public class PlaylistServiceTests
{
    [Fact]
    public void Add_ShouldInsertItemIntoPlaylist()
    {
        var service = new PlaylistService(NullLogger<PlaylistService>.Instance);
        service.Add(@"C:\Videos\video1.mp4");

        Assert.Single(service.Items);
        Assert.Equal("video1.mp4", service.Items[0].Title);
        Assert.Equal(@"C:\Videos\video1.mp4", service.Items[0].FilePath);
    }

    [Fact]
    public void AddRange_ShouldInsertMultipleItems()
    {
        var service = new PlaylistService(NullLogger<PlaylistService>.Instance);
        service.AddRange(new[] { @"C:\Videos\v1.mp4", @"C:\Videos\v2.mkv", @"C:\Videos\v3.avi" });

        Assert.Equal(3, service.Items.Count);
        Assert.Equal("v1.mp4", service.Items[0].Title);
        Assert.Equal("v2.mkv", service.Items[1].Title);
        Assert.Equal("v3.avi", service.Items[2].Title);
    }

    [Fact]
    public void Remove_ShouldRemoveItemAndAdjustIndex()
    {
        var service = new PlaylistService(NullLogger<PlaylistService>.Instance);
        service.AddRange(new[] { @"C:\Videos\v1.mp4", @"C:\Videos\v2.mp4", @"C:\Videos\v3.mp4" });

        service.PlayAt(1); // v2 playing
        Assert.Equal("v2.mp4", service.CurrentItem?.Title);

        service.Remove(service.Items[0]); // remove v1
        Assert.Equal(2, service.Items.Count);
        Assert.Equal("v2.mp4", service.CurrentItem?.Title);
        Assert.Equal(0, service.CurrentIndex);
    }

    [Fact]
    public void MoveItem_ShouldReorderCorrectly()
    {
        var service = new PlaylistService(NullLogger<PlaylistService>.Instance);
        service.AddRange(new[] { @"C:\Videos\v1.mp4", @"C:\Videos\v2.mp4", @"C:\Videos\v3.mp4" });

        service.MoveItem(0, 2); // move v1 to end: [v2, v3, v1]

        Assert.Equal("v2.mp4", service.Items[0].Title);
        Assert.Equal("v3.mp4", service.Items[1].Title);
        Assert.Equal("v1.mp4", service.Items[2].Title);
    }

    [Fact]
    public void NextAndPrevious_NormalMode_ShouldAdvanceAndGoBack()
    {
        var service = new PlaylistService(NullLogger<PlaylistService>.Instance);
        service.AddRange(new[] { @"C:\Videos\v1.mp4", @"C:\Videos\v2.mp4", @"C:\Videos\v3.mp4" });

        service.PlayAt(0);
        var next = service.Next();
        Assert.Equal("v2.mp4", next?.Title);

        next = service.Next();
        Assert.Equal("v3.mp4", next?.Title);

        // At end without repeat
        next = service.Next();
        Assert.Null(next);

        var prev = service.Previous();
        Assert.Equal("v2.mp4", prev?.Title);
    }

    [Fact]
    public void Next_RepeatAll_ShouldLoopBackToFirstItem()
    {
        var service = new PlaylistService(NullLogger<PlaylistService>.Instance)
        {
            RepeatMode = RepeatMode.All
        };
        service.AddRange(new[] { @"C:\Videos\v1.mp4", @"C:\Videos\v2.mp4" });

        service.PlayAt(1); // at last item
        var next = service.Next();

        Assert.NotNull(next);
        Assert.Equal("v1.mp4", next.Title);
    }

    [Fact]
    public void Next_RepeatOne_ShouldReturnSameItem()
    {
        var service = new PlaylistService(NullLogger<PlaylistService>.Instance)
        {
            RepeatMode = RepeatMode.One
        };
        service.AddRange(new[] { @"C:\Videos\v1.mp4", @"C:\Videos\v2.mp4" });

        service.PlayAt(0);
        var next = service.Next();

        Assert.NotNull(next);
        Assert.Equal("v1.mp4", next.Title);
    }

    [Fact]
    public async Task SaveAndLoadM3u_ShouldRoundtripPlaylist()
    {
        var service = new PlaylistService(NullLogger<PlaylistService>.Instance);
        string tempFile1 = Path.GetTempFileName();
        string tempFile2 = Path.GetTempFileName();
        string m3uFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.m3u");

        try
        {
            service.Add(tempFile1);
            service.Add(tempFile2);

            await service.SaveM3uAsync(m3uFile);
            Assert.True(File.Exists(m3uFile));

            var newService = new PlaylistService(NullLogger<PlaylistService>.Instance);
            await newService.LoadM3uAsync(m3uFile);

            Assert.Equal(2, newService.Items.Count);
            Assert.Equal(tempFile1, newService.Items[0].FilePath);
            Assert.Equal(tempFile2, newService.Items[1].FilePath);
        }
        finally
        {
            if (File.Exists(tempFile1)) File.Delete(tempFile1);
            if (File.Exists(tempFile2)) File.Delete(tempFile2);
            if (File.Exists(m3uFile)) File.Delete(m3uFile);
        }
    }
}
