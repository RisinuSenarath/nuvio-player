namespace NuvioPlayer.Interfaces;

using NuvioPlayer.Models;

public interface ITmdbService
{
    Task<List<MediaCatalogItem>> GetTrendingAsync(int page = 1, CancellationToken cancellationToken = default);
    Task<List<MediaCatalogItem>> GetPopularMoviesAsync(int page = 1, CancellationToken cancellationToken = default);
    Task<List<MediaCatalogItem>> GetPopularTvShowsAsync(int page = 1, CancellationToken cancellationToken = default);
    Task<List<MediaCatalogItem>> SearchMultiAsync(string query, int page = 1, CancellationToken cancellationToken = default);
    Task<MediaCatalogItem?> GetDetailsAsync(int tmdbId, string mediaType, CancellationToken cancellationToken = default);
    Task<List<TvSeason>> GetTvSeasonsAsync(int tmdbId, CancellationToken cancellationToken = default);
    Task<List<TvEpisode>> GetSeasonEpisodesAsync(int tmdbId, int seasonNumber, CancellationToken cancellationToken = default);
}
