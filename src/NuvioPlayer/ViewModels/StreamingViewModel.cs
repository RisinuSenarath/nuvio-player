namespace NuvioPlayer.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;

public partial class StreamingViewModel : ViewModelBase
{
    private readonly ITmdbService _tmdbService;
    private readonly IDirectStreamResolverService _streamResolver;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isStreamsLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _activeTab = "trending";

    [ObservableProperty]
    private MediaCatalogItem? _selectedItem;

    [ObservableProperty]
    private TvSeason? _selectedSeason;

    [ObservableProperty]
    private TvEpisode? _selectedEpisode;

    [ObservableProperty]
    private DirectStreamSource? _selectedStream;

    [ObservableProperty]
    private bool _hasStreams;

    [ObservableProperty]
    private bool _hasNoStreams;

    [ObservableProperty]
    private string? _fallbackEmbedUrl;

    [ObservableProperty]
    private string _selectedProvider = "vidlink";

    public ObservableCollection<MediaCatalogItem> CatalogItems { get; } = new();
    public ObservableCollection<TvSeason> Seasons { get; } = new();
    public ObservableCollection<TvEpisode> Episodes { get; } = new();
    public ObservableCollection<DirectStreamSource> AvailableStreams { get; } = new();

    public event Action<string, string>? PlayRequested;
    public event Action<int, string, int?, int?, string, string>? PlayWebStreamRequested;
    public event Action? CloseRequested;

    public StreamingViewModel(ITmdbService tmdbService, IDirectStreamResolverService streamResolver)
    {
        _tmdbService = tmdbService;
        _streamResolver = streamResolver;
    }

    public async Task InitializeAsync()
    {
        await LoadCategoryAsync("trending");
    }

    [RelayCommand]
    public async Task SelectTab(string tab)
    {
        if (ActiveTab == tab && string.IsNullOrWhiteSpace(SearchQuery)) return;

        ActiveTab = tab;
        SearchQuery = string.Empty;
        SelectedItem = null;
        await LoadCategoryAsync(tab);
    }

    [RelayCommand]
    public async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await LoadCategoryAsync(ActiveTab);
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        IsLoading = true;
        StatusMessage = $"Searching for \"{SearchQuery}\"...";
        SelectedItem = null;

        try
        {
            var results = await _tmdbService.SearchMultiAsync(SearchQuery, 1, _searchCts.Token);
            CatalogItems.Clear();
            foreach (var item in results)
            {
                CatalogItems.Add(item);
            }

            StatusMessage = CatalogItems.Count > 0
                ? $"Found {CatalogItems.Count} titles for \"{SearchQuery}\""
                : $"No results found for \"{SearchQuery}\"";
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ClearSearch()
    {
        SearchQuery = string.Empty;
        await LoadCategoryAsync(ActiveTab);
    }

    [RelayCommand]
    public async Task SelectItem(MediaCatalogItem item)
    {
        SelectedItem = item;
        AvailableStreams.Clear();
        HasStreams = false;
        HasNoStreams = false;
        Seasons.Clear();
        Episodes.Clear();
        SelectedSeason = null;
        SelectedEpisode = null;

        if (item.MediaType == "tv")
        {
            IsLoading = true;
            StatusMessage = "Loading TV seasons...";
            try
            {
                var seasons = await _tmdbService.GetTvSeasonsAsync(item.Id);
                foreach (var s in seasons)
                {
                    Seasons.Add(s);
                }

                if (Seasons.Count > 0)
                {
                    await SelectSeason(Seasons[0]);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
        else
        {
            // Movie - resolve streams directly
            await FetchStreamsForCurrentSelectionAsync();
        }
    }

    [RelayCommand]
    public void BackToCatalog()
    {
        SelectedItem = null;
        AvailableStreams.Clear();
        HasStreams = false;
        HasNoStreams = false;
    }

    [RelayCommand]
    public async Task SelectSeason(TvSeason season)
    {
        SelectedSeason = season;
        Episodes.Clear();
        SelectedEpisode = null;
        AvailableStreams.Clear();
        HasStreams = false;
        HasNoStreams = false;

        if (SelectedItem == null) return;

        IsLoading = true;
        StatusMessage = $"Loading {season.DisplayName} episodes...";
        try
        {
            var eps = await _tmdbService.GetSeasonEpisodesAsync(SelectedItem.Id, season.SeasonNumber);
            foreach (var ep in eps)
            {
                Episodes.Add(ep);
            }

            if (Episodes.Count > 0)
            {
                await SelectEpisode(Episodes[0]);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SelectEpisode(TvEpisode episode)
    {
        SelectedEpisode = episode;
        await FetchStreamsForCurrentSelectionAsync();
    }

    [RelayCommand]
    public void PlayStream(DirectStreamSource? stream)
    {
        var targetStream = stream ?? SelectedStream;
        if (targetStream == null || string.IsNullOrWhiteSpace(targetStream.Url)) return;

        string title = SelectedItem != null
            ? (SelectedItem.MediaType == "tv" && SelectedEpisode != null
                ? $"{SelectedItem.Title} - {SelectedEpisode.EpisodeCode}: {SelectedEpisode.Title} [{targetStream.Quality}]"
                : $"{SelectedItem.Title} ({SelectedItem.ReleaseYear}) [{targetStream.Quality}]")
            : targetStream.Name;

        PlayRequested?.Invoke(targetStream.Url, title);
    }

    [RelayCommand]
    public void SelectProvider(string provider)
    {
        SelectedProvider = provider;
        if (SelectedItem != null)
        {
            FallbackEmbedUrl = _streamResolver.GetEmbedFallbackUrl(
                SelectedItem.Id,
                SelectedItem.MediaType,
                SelectedSeason?.SeasonNumber,
                SelectedEpisode?.EpisodeNumber,
                provider);
        }
    }

    [RelayCommand]
    public void PlayEmbedFallback()
    {
        if (string.IsNullOrWhiteSpace(FallbackEmbedUrl)) return;

        string title = SelectedItem != null
            ? (SelectedItem.MediaType == "tv" && SelectedEpisode != null
                ? $"{SelectedItem.Title} - {SelectedEpisode.EpisodeCode}: {SelectedEpisode.Title}"
                : $"{SelectedItem.Title} ({SelectedItem.ReleaseYear})")
            : "Online Stream";

        if (SelectedItem != null && PlayWebStreamRequested != null)
        {
            PlayWebStreamRequested.Invoke(
                SelectedItem.Id,
                SelectedItem.MediaType,
                SelectedSeason?.SeasonNumber,
                SelectedEpisode?.EpisodeNumber,
                title,
                SelectedProvider);
        }
        else
        {
            PlayRequested?.Invoke(FallbackEmbedUrl, title);
        }
    }

    [RelayCommand]
    public void OpenInBrowser()
    {
        if (string.IsNullOrWhiteSpace(FallbackEmbedUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(FallbackEmbedUrl) { UseShellExecute = true });
        }
        catch
        {
            // Ignore browser launch failure
        }
    }

    [RelayCommand]
    public void Close()
    {
        CloseRequested?.Invoke();
    }

    private async Task FetchStreamsForCurrentSelectionAsync()
    {
        if (SelectedItem == null) return;

        IsStreamsLoading = true;
        HasStreams = false;
        HasNoStreams = false;
        AvailableStreams.Clear();

        int tmdbId = SelectedItem.Id;
        string mediaType = SelectedItem.MediaType;
        int? season = SelectedSeason?.SeasonNumber;
        int? episode = SelectedEpisode?.EpisodeNumber;

        FallbackEmbedUrl = _streamResolver.GetEmbedFallbackUrl(tmdbId, mediaType, season, episode, SelectedProvider);

        try
        {
            var streams = await _streamResolver.ResolveStreamsAsync(tmdbId, mediaType, season, episode);
            foreach (var s in streams)
            {
                AvailableStreams.Add(s);
            }

            HasStreams = AvailableStreams.Count > 0;
            HasNoStreams = !HasStreams;

            if (HasStreams)
            {
                SelectedStream = AvailableStreams[0];
            }
        }
        catch (Exception ex)
        {
            HasNoStreams = true;
            StatusMessage = $"Stream resolution note: {ex.Message}";
        }
        finally
        {
            IsStreamsLoading = false;
        }
    }

    private async Task LoadCategoryAsync(string category)
    {
        IsLoading = true;
        StatusMessage = "Loading titles...";
        CatalogItems.Clear();

        try
        {
            List<MediaCatalogItem> items = category switch
            {
                "movies" => await _tmdbService.GetPopularMoviesAsync(1),
                "tv" => await _tmdbService.GetPopularTvShowsAsync(1),
                _ => await _tmdbService.GetTrendingAsync(1)
            };

            foreach (var item in items)
            {
                CatalogItems.Add(item);
            }

            StatusMessage = $"Showing {CatalogItems.Count} titles";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading titles: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
