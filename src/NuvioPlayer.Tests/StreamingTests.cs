using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NuvioPlayer.Converters;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;
using NuvioPlayer.Services;
using NuvioPlayer.ViewModels;
using Xunit;

namespace NuvioPlayer.Tests;

public class StreamingTests
{
    [Theory]
    [InlineData(null, Visibility.Collapsed)]
    [InlineData("", Visibility.Collapsed)]
    [InlineData("   ", Visibility.Collapsed)]
    [InlineData("Valid", Visibility.Visible)]
    public void NullToVisibilityConverter_ShouldConvertCorrectly(object? input, Visibility expected)
    {
        var converter = new NullToVisibilityConverter();
        var result = converter.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, Visibility.Visible)]
    [InlineData("", Visibility.Visible)]
    [InlineData("Valid", Visibility.Collapsed)]
    public void NullToVisibilityConverter_Invert_ShouldInvertCorrectly(object? input, Visibility expected)
    {
        var converter = new NullToVisibilityConverter { Invert = true };
        var result = converter.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DirectStreamResolver_GetEmbedFallbackUrl_ShouldGenerateValidVidLinkUrl()
    {
        var resolver = new DirectStreamResolverService();

        // Movie
        string movieUrl = resolver.GetEmbedFallbackUrl(550, "movie", null, null, "vidlink");
        Assert.Contains("vidlink.pro/movie/550", movieUrl);
        Assert.Contains("primaryColor=38bdf8", movieUrl);

        // TV Show
        string tvUrl = resolver.GetEmbedFallbackUrl(1399, "tv", 2, 5, "vidlink");
        Assert.Contains("vidlink.pro/tv/1399/2/5", tvUrl);
        Assert.Contains("primaryColor=38bdf8", tvUrl);
    }

    [Fact]
    public void DirectStreamResolver_GetEmbedFallbackUrl_ShouldSupportOtherProviders()
    {
        var resolver = new DirectStreamResolverService();

        string vidsrcMovie = resolver.GetEmbedFallbackUrl(550, "movie", null, null, "vidsrc");
        Assert.Contains("vidsrc.cc/v2/embed/movie/550", vidsrcMovie);

        string vidsrcTv = resolver.GetEmbedFallbackUrl(1399, "tv", 1, 3, "vidsrc");
        Assert.Contains("vidsrc.cc/v2/embed/tv/1399/1/3", vidsrcTv);

        string superEmbed = resolver.GetEmbedFallbackUrl(550, "movie", null, null, "superembed");
        Assert.Contains("multiembed.mov/?video_id=550&tmdb=1", superEmbed);
    }

    [Fact]
    public async Task StreamingViewModel_SelectTab_ShouldUpdateActiveTab()
    {
        var tmdbMock = new MockTmdbService();
        var streamMock = new MockStreamResolver();
        var vm = new StreamingViewModel(tmdbMock, streamMock);

        await vm.SelectTab("movies");

        Assert.Equal("movies", vm.ActiveTab);
        Assert.Equal(1, tmdbMock.PopularMoviesCallCount);
    }

    [Fact]
    public async Task StreamingViewModel_Search_ShouldCallSearchMulti()
    {
        var tmdbMock = new MockTmdbService();
        var streamMock = new MockStreamResolver();
        var vm = new StreamingViewModel(tmdbMock, streamMock)
        {
            SearchQuery = "Inception"
        };

        await vm.Search();

        Assert.Equal(1, tmdbMock.SearchCallCount);
        Assert.Single(vm.CatalogItems);
        Assert.Equal("Inception", vm.CatalogItems[0].Title);
    }

    [Fact]
    public async Task StreamingViewModel_SelectItem_Movie_ShouldResolveStreams()
    {
        var tmdbMock = new MockTmdbService();
        var streamMock = new MockStreamResolver();
        var vm = new StreamingViewModel(tmdbMock, streamMock);

        var movieItem = new MediaCatalogItem
        {
            Id = 100,
            Title = "Interstellar",
            MediaType = "movie",
            ReleaseYear = "2014"
        };

        await vm.SelectItem(movieItem);

        Assert.NotNull(vm.SelectedItem);
        Assert.Equal(1, streamMock.ResolveStreamsCallCount);
        Assert.True(vm.HasStreams);
        Assert.Single(vm.AvailableStreams);
        Assert.Equal("1080p", vm.AvailableStreams[0].Quality);
    }

    [Fact]
    public async Task StreamingViewModel_SelectItem_Tv_ShouldLoadSeasons()
    {
        var tmdbMock = new MockTmdbService();
        var streamMock = new MockStreamResolver();
        var vm = new StreamingViewModel(tmdbMock, streamMock);

        var tvItem = new MediaCatalogItem
        {
            Id = 200,
            Title = "Breaking Bad",
            MediaType = "tv",
            ReleaseYear = "2008"
        };

        await vm.SelectItem(tvItem);

        Assert.NotNull(vm.SelectedItem);
        Assert.Equal(1, tmdbMock.TvSeasonsCallCount);
        Assert.Single(vm.Seasons);
        Assert.Equal("Season 1", vm.Seasons[0].DisplayName);
        Assert.Single(vm.Episodes);
        Assert.Equal("Pilot", vm.Episodes[0].Title);
        Assert.True(vm.HasStreams);
    }

    [Fact]
    public void StreamingViewModel_PlayStream_ShouldRaisePlayRequestedEvent()
    {
        var tmdbMock = new MockTmdbService();
        var streamMock = new MockStreamResolver();
        var vm = new StreamingViewModel(tmdbMock, streamMock);

        string? playedUrl = null;
        string? playedTitle = null;

        vm.PlayRequested += (url, title) =>
        {
            playedUrl = url;
            playedTitle = title;
        };

        var stream = new DirectStreamSource
        {
            Name = "Direct Stream 1080p",
            Url = "https://cdn.example.com/stream.mp4",
            Quality = "1080p"
        };

        vm.PlayStream(stream);

        Assert.Equal("https://cdn.example.com/stream.mp4", playedUrl);
        Assert.Equal("Direct Stream 1080p", playedTitle);
    }

    [Fact]
    public void StreamingViewModel_PlayEmbedFallback_ShouldRaisePlayRequestedEvent()
    {
        var tmdbMock = new MockTmdbService();
        var streamMock = new MockStreamResolver();
        var vm = new StreamingViewModel(tmdbMock, streamMock);

        string? playedUrl = null;
        string? playedTitle = null;

        vm.PlayRequested += (url, title) =>
        {
            playedUrl = url;
            playedTitle = title;
        };

        vm.FallbackEmbedUrl = "https://vidlink.pro/movie/550";
        vm.PlayEmbedFallback();

        Assert.Equal("https://vidlink.pro/movie/550", playedUrl);
        Assert.Equal("Online Stream", playedTitle);
    }

    [Fact]
    public void StreamingViewModel_BackToCatalog_ShouldResetSelection()
    {
        var tmdbMock = new MockTmdbService();
        var streamMock = new MockStreamResolver();
        var vm = new StreamingViewModel(tmdbMock, streamMock)
        {
            SelectedItem = new MediaCatalogItem { Id = 1, Title = "Test" }
        };

        vm.BackToCatalog();

        Assert.Null(vm.SelectedItem);
        Assert.False(vm.HasStreams);
    }
}

public class MockTmdbService : ITmdbService
{
    public int TrendingCallCount { get; set; }
    public int PopularMoviesCallCount { get; set; }
    public int PopularTvCallCount { get; set; }
    public int SearchCallCount { get; set; }
    public int TvSeasonsCallCount { get; set; }
    public int SeasonEpisodesCallCount { get; set; }

    public Task<List<MediaCatalogItem>> GetTrendingAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        TrendingCallCount++;
        return Task.FromResult(new List<MediaCatalogItem>
        {
            new() { Id = 1, Title = "Trending Title", MediaType = "movie" }
        });
    }

    public Task<List<MediaCatalogItem>> GetPopularMoviesAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        PopularMoviesCallCount++;
        return Task.FromResult(new List<MediaCatalogItem>
        {
            new() { Id = 2, Title = "Popular Movie", MediaType = "movie" }
        });
    }

    public Task<List<MediaCatalogItem>> GetPopularTvShowsAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        PopularTvCallCount++;
        return Task.FromResult(new List<MediaCatalogItem>
        {
            new() { Id = 3, Title = "Popular TV Show", MediaType = "tv" }
        });
    }

    public Task<List<MediaCatalogItem>> SearchMultiAsync(string query, int page = 1, CancellationToken cancellationToken = default)
    {
        SearchCallCount++;
        return Task.FromResult(new List<MediaCatalogItem>
        {
            new() { Id = 4, Title = query, MediaType = "movie" }
        });
    }

    public Task<MediaCatalogItem?> GetDetailsAsync(int id, string mediaType, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<MediaCatalogItem?>(new MediaCatalogItem { Id = id, Title = "Details", MediaType = mediaType });
    }

    public Task<List<TvSeason>> GetTvSeasonsAsync(int tvShowId, CancellationToken cancellationToken = default)
    {
        TvSeasonsCallCount++;
        return Task.FromResult(new List<TvSeason>
        {
            new() { SeasonNumber = 1, Name = "Season 1", EpisodeCount = 7 }
        });
    }

    public Task<List<TvEpisode>> GetSeasonEpisodesAsync(int tvShowId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        SeasonEpisodesCallCount++;
        return Task.FromResult(new List<TvEpisode>
        {
            new() { EpisodeNumber = 1, SeasonNumber = seasonNumber, Title = "Pilot" }
        });
    }
}

public class MockStreamResolver : IDirectStreamResolverService
{
    public int ResolveStreamsCallCount { get; set; }

    public Task<List<DirectStreamSource>> ResolveStreamsAsync(int tmdbId, string mediaType, int? season = null, int? episode = null, CancellationToken cancellationToken = default)
    {
        ResolveStreamsCallCount++;
        return Task.FromResult(new List<DirectStreamSource>
        {
            new()
            {
                Name = "Direct Stream 1080p",
                Url = "https://cdn.example.com/stream1080p.mp4",
                Quality = "1080p",
                Size = "2.2 GB",
                Format = "Direct HTTP / MP4"
            }
        });
    }

    public string GetEmbedFallbackUrl(int tmdbId, string mediaType, int? season = null, int? episode = null, string provider = "vidlink")
    {
        return $"https://vidlink.pro/{mediaType}/{tmdbId}";
    }
}
