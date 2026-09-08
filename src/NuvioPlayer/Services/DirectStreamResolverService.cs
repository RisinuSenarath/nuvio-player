namespace NuvioPlayer.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Models;

public class DirectStreamResolverService : IDirectStreamResolverService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DirectStreamResolverService>? _logger;

    private static readonly string[] StreamEndpoints = Array.Empty<string>();

    public DirectStreamResolverService(HttpClient? httpClient = null, ILogger<DirectStreamResolverService>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _logger = logger;
    }

    public async Task<List<DirectStreamSource>> ResolveStreamsAsync(
        int tmdbId,
        string mediaType,
        int? season = null,
        int? episode = null,
        CancellationToken cancellationToken = default)
    {
        if (StreamEndpoints.Length == 0)
        {
            return new List<DirectStreamSource>();
        }

        bool isTv = mediaType == "tv" || mediaType == "series";
        string type = isTv ? "series" : "movie";
        string streamId = isTv && season.HasValue && episode.HasValue
            ? $"tmdb:{tmdbId}:{season.Value}:{episode.Value}"
            : $"tmdb:{tmdbId}";

        var tasks = StreamEndpoints.Select(async baseEndpoint =>
        {
            string url = $"{baseEndpoint}/stream/{type}/{streamId}.json";
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                string json = await _httpClient.GetStringAsync(url, cts.Token);
                var root = JsonNode.Parse(json);
                var streamArray = root?["streams"]?.AsArray();
                if (streamArray == null) return Enumerable.Empty<DirectStreamSource>();

                var list = new List<DirectStreamSource>();
                foreach (var item in streamArray)
                {
                    if (item == null) continue;
                    string? streamUrl = item["url"]?.GetValue<string>();
                    string? externalUrl = item["externalUrl"]?.GetValue<string>();
                    string? effectiveUrl = !string.IsNullOrWhiteSpace(streamUrl) ? streamUrl : externalUrl;

                    if (string.IsNullOrWhiteSpace(effectiveUrl)) continue;

                    string name = item["name"]?.GetValue<string>() ?? "Stream";
                    string title = item["title"]?.GetValue<string>() ?? string.Empty;

                    string quality = DetectQuality(name, title);
                    string? size = DetectSize(title);

                    list.Add(new DirectStreamSource
                    {
                        Name = name,
                        Title = title,
                        Quality = quality,
                        Size = size,
                        Url = effectiveUrl,
                        ExternalUrl = externalUrl,
                        IsDirectStream = effectiveUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    });
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("Failed to fetch streams from {Endpoint}: {Message}", baseEndpoint, ex.Message);
                return Enumerable.Empty<DirectStreamSource>();
            }
        });

        var results = await Task.WhenAll(tasks);
        var allStreams = results.SelectMany(s => s).ToList();

        // Deduplicate by URL
        var uniqueStreams = allStreams
            .GroupBy(s => s.Url)
            .Select(g => g.First())
            .OrderByDescending(s => GetQualityRank(s.Quality))
            .ToList();

        return uniqueStreams;
    }

    public string GetEmbedFallbackUrl(
        int tmdbId,
        string mediaType,
        int? season = null,
        int? episode = null,
        string provider = "vidlink")
    {
        bool isTv = mediaType == "tv" || mediaType == "series";
        int s = season ?? 1;
        int ep = episode ?? 1;
        const string primaryColor = "38bdf8";

        return provider.ToLowerInvariant() switch
        {
            "superembed" or "multiembed" => isTv
                ? $"https://multiembed.mov/?video_id={tmdbId}&tmdb=1&s={s}&e={ep}"
                : $"https://multiembed.mov/?video_id={tmdbId}&tmdb=1",

            "vidsrc-v3" => isTv
                ? $"https://vidsrc.cc/v3/embed/tv/{tmdbId}/{s}/{ep}?autoPlay=true"
                : $"https://vidsrc.cc/v3/embed/movie/{tmdbId}?autoPlay=true",

            "vidsrc" => isTv
                ? $"https://vidsrc.cc/v2/embed/tv/{tmdbId}/{s}/{ep}?autoPlay=true"
                : $"https://vidsrc.cc/v2/embed/movie/{tmdbId}?autoPlay=true",

            "vidking" => isTv
                ? $"https://www.vidking.net/embed/tv/{tmdbId}/{s}/{ep}?color={primaryColor}&autoplay=true"
                : $"https://www.vidking.net/embed/movie/{tmdbId}?color={primaryColor}&autoplay=true",

            "autoembed" => isTv
                ? $"https://player.autoembed.cc/embed/tv/{tmdbId}/{s}/{ep}"
                : $"https://player.autoembed.cc/embed/movie/{tmdbId}",

            _ => isTv // Default: VidLink (with JWPlayer)
                ? $"https://vidlink.pro/tv/{tmdbId}/{s}/{ep}?player=jw&primaryColor={primaryColor}&autoplay=true"
                : $"https://vidlink.pro/movie/{tmdbId}?player=jw&primaryColor={primaryColor}&autoplay=true"
        };
    }

    private static string DetectQuality(string name, string title)
    {
        string combined = $"{name} {title}".ToUpperInvariant();

        if (combined.Contains("4K") || combined.Contains("2160P") || combined.Contains("UHD"))
            return "4K";
        if (combined.Contains("1080P") || combined.Contains("FHD"))
            return "1080p";
        if (combined.Contains("720P") || combined.Contains("HD"))
            return "720p";
        if (combined.Contains("480P") || combined.Contains("SD"))
            return "480p";

        return "HD";
    }

    private static string? DetectSize(string title)
    {
        var match = Regex.Match(title, @"(\d+(?:\.\d+)?\s*(?:GB|MB))", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static int GetQualityRank(string quality)
    {
        return quality.ToUpperInvariant() switch
        {
            "4K" => 400,
            "2160P" => 400,
            "1080P" => 300,
            "720P" => 200,
            "HD" => 150,
            "480P" => 100,
            _ => 50
        };
    }
}
