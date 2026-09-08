namespace NuvioPlayer.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;

public class TmdbService : ITmdbService
{
    private const string ApiKey = "aeeab0e68cd1830cf027fb676d944a79";
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string PosterBaseUrl = "https://image.tmdb.org/t/p/w500";
    private const string BackdropBaseUrl = "https://image.tmdb.org/t/p/original";
    private const string StillBaseUrl = "https://image.tmdb.org/t/p/w300";

    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbService>? _logger;

    private static readonly Dictionary<int, string> GenreMap = new()
    {
        { 28, "Action" }, { 12, "Adventure" }, { 16, "Animation" }, { 35, "Comedy" },
        { 80, "Crime" }, { 99, "Documentary" }, { 18, "Drama" }, { 10751, "Family" },
        { 14, "Fantasy" }, { 36, "History" }, { 27, "Horror" }, { 10402, "Music" },
        { 9648, "Mystery" }, { 10749, "Romance" }, { 878, "Sci-Fi" }, { 10770, "TV Movie" },
        { 53, "Thriller" }, { 10752, "War" }, { 37, "Western" }, { 10759, "Action & Adventure" },
        { 10762, "Kids" }, { 10763, "News" }, { 10764, "Reality" }, { 10765, "Sci-Fi & Fantasy" },
        { 10766, "Soap" }, { 10767, "Talk" }, { 10768, "War & Politics" }
    };

    public TmdbService(HttpClient? httpClient = null, ILogger<TmdbService>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger;
    }

    public async Task<List<MediaCatalogItem>> GetTrendingAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        string url = $"{BaseUrl}/trending/all/week?api_key={ApiKey}&page={page}";
        return await FetchCatalogListAsync(url, null, cancellationToken);
    }

    public async Task<List<MediaCatalogItem>> GetPopularMoviesAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        string url = $"{BaseUrl}/movie/popular?api_key={ApiKey}&language=en-US&page={page}";
        return await FetchCatalogListAsync(url, "movie", cancellationToken);
    }

    public async Task<List<MediaCatalogItem>> GetPopularTvShowsAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        string url = $"{BaseUrl}/tv/popular?api_key={ApiKey}&language=en-US&page={page}";
        return await FetchCatalogListAsync(url, "tv", cancellationToken);
    }

    public async Task<List<MediaCatalogItem>> SearchMultiAsync(string query, int page = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<MediaCatalogItem>();
        }

        string encoded = Uri.EscapeDataString(query.Trim());
        string url = $"{BaseUrl}/search/multi?api_key={ApiKey}&language=en-US&query={encoded}&page={page}&include_adult=false";
        return await FetchCatalogListAsync(url, null, cancellationToken);
    }

    public async Task<MediaCatalogItem?> GetDetailsAsync(int tmdbId, string mediaType, CancellationToken cancellationToken = default)
    {
        string type = mediaType == "tv" ? "tv" : "movie";
        string url = $"{BaseUrl}/{type}/{tmdbId}?api_key={ApiKey}&language=en-US";

        try
        {
            string json = await _httpClient.GetStringAsync(url, cancellationToken);
            var node = JsonNode.Parse(json);
            if (node == null) return null;

            return ParseCatalogItem(node, type);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch TMDB details for {Type} {Id}", mediaType, tmdbId);
            return null;
        }
    }

    public async Task<List<TvSeason>> GetTvSeasonsAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        string url = $"{BaseUrl}/tv/{tmdbId}?api_key={ApiKey}&language=en-US";
        var list = new List<TvSeason>();

        try
        {
            string json = await _httpClient.GetStringAsync(url, cancellationToken);
            var node = JsonNode.Parse(json);
            var seasonsArray = node?["seasons"]?.AsArray();
            if (seasonsArray == null) return list;

            foreach (var sNode in seasonsArray)
            {
                if (sNode == null) continue;
                int seasonNumber = sNode["season_number"]?.GetValue<int>() ?? 0;
                if (seasonNumber <= 0) continue; // Skip specials by default

                string? posterPath = sNode["poster_path"]?.GetValue<string>();
                list.Add(new TvSeason
                {
                    SeasonNumber = seasonNumber,
                    Name = sNode["name"]?.GetValue<string>() ?? $"Season {seasonNumber}",
                    EpisodeCount = sNode["episode_count"]?.GetValue<int>() ?? 0,
                    PosterUrl = !string.IsNullOrEmpty(posterPath) ? $"{PosterBaseUrl}{posterPath}" : null,
                    Overview = sNode["overview"]?.GetValue<string>()
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch TV seasons for TMDB {Id}", tmdbId);
        }

        return list;
    }

    public async Task<List<TvEpisode>> GetSeasonEpisodesAsync(int tmdbId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        string url = $"{BaseUrl}/tv/{tmdbId}/season/{seasonNumber}?api_key={ApiKey}&language=en-US";
        var list = new List<TvEpisode>();

        try
        {
            string json = await _httpClient.GetStringAsync(url, cancellationToken);
            var node = JsonNode.Parse(json);
            var episodesArray = node?["episodes"]?.AsArray();
            if (episodesArray == null) return list;

            foreach (var eNode in episodesArray)
            {
                if (eNode == null) continue;
                string? stillPath = eNode["still_path"]?.GetValue<string>();

                list.Add(new TvEpisode
                {
                    EpisodeNumber = eNode["episode_number"]?.GetValue<int>() ?? 0,
                    SeasonNumber = seasonNumber,
                    Title = eNode["name"]?.GetValue<string>() ?? $"Episode {eNode["episode_number"]}",
                    Overview = eNode["overview"]?.GetValue<string>() ?? string.Empty,
                    StillUrl = !string.IsNullOrEmpty(stillPath) ? $"{StillBaseUrl}{stillPath}" : null,
                    AirDate = eNode["air_date"]?.GetValue<string>() ?? string.Empty,
                    VoteAverage = eNode["vote_average"]?.GetValue<double>() ?? 0.0
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch TV episodes for TMDB {Id} S{Season}", tmdbId, seasonNumber);
        }

        return list;
    }

    private async Task<List<MediaCatalogItem>> FetchCatalogListAsync(string url, string? forcedType, CancellationToken cancellationToken)
    {
        var results = new List<MediaCatalogItem>();

        try
        {
            string json = await _httpClient.GetStringAsync(url, cancellationToken);
            var root = JsonNode.Parse(json);
            var items = root?["results"]?.AsArray();
            if (items == null) return results;

            foreach (var item in items)
            {
                if (item == null) continue;
                var parsed = ParseCatalogItem(item, forcedType);
                if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Title))
                {
                    results.Add(parsed);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error fetching catalog from {Url}", url);
        }

        return results;
    }

    private static MediaCatalogItem? ParseCatalogItem(JsonNode node, string? forcedType)
    {
        string mediaType = forcedType ?? node["media_type"]?.GetValue<string>() ?? "movie";
        if (mediaType == "person") return null;

        int id = node["id"]?.GetValue<int>() ?? 0;
        if (id == 0) return null;

        string title = node["title"]?.GetValue<string>()
                       ?? node["name"]?.GetValue<string>()
                       ?? "Untitled";

        string? originalTitle = node["original_title"]?.GetValue<string>()
                                ?? node["original_name"]?.GetValue<string>();

        string overview = node["overview"]?.GetValue<string>() ?? string.Empty;
        string? posterPath = node["poster_path"]?.GetValue<string>();
        string? backdropPath = node["backdrop_path"]?.GetValue<string>();
        double rating = node["vote_average"]?.GetValue<double>() ?? 0.0;
        int voteCount = node["vote_count"]?.GetValue<int>() ?? 0;

        string releaseDate = node["release_date"]?.GetValue<string>()
                             ?? node["first_air_date"]?.GetValue<string>()
                             ?? string.Empty;

        string releaseYear = releaseDate.Length >= 4 ? releaseDate[..4] : string.Empty;

        var genres = new List<string>();
        if (node["genres"] is JsonArray fullGenres)
        {
            foreach (var g in fullGenres)
            {
                string? gName = g?["name"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(gName)) genres.Add(gName);
            }
        }
        else if (node["genre_ids"] is JsonArray genreIds)
        {
            foreach (var gid in genreIds)
            {
                if (gid != null && GenreMap.TryGetValue(gid.GetValue<int>(), out string? gName))
                {
                    genres.Add(gName);
                }
            }
        }

        return new MediaCatalogItem
        {
            Id = id,
            Title = title,
            OriginalTitle = originalTitle,
            MediaType = mediaType,
            Overview = overview,
            PosterUrl = !string.IsNullOrEmpty(posterPath) ? $"{PosterBaseUrl}{posterPath}" : null,
            BackdropUrl = !string.IsNullOrEmpty(backdropPath) ? $"{BackdropBaseUrl}{backdropPath}" : null,
            Rating = Math.Round(rating, 1),
            ReleaseDate = releaseDate,
            ReleaseYear = releaseYear,
            VoteCount = voteCount,
            Genres = genres
        };
    }
}
