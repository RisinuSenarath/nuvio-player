namespace NuvioPlayer.Models;

public class MediaCatalogItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public string MediaType { get; set; } = "movie"; // "movie" or "tv"
    public string Overview { get; set; } = string.Empty;
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public double Rating { get; set; }
    public double VoteAverage => Rating;
    public string ReleaseYear { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public int VoteCount { get; set; }
    public List<string> Genres { get; set; } = new();

    public string DisplaySubtitle => $"{ReleaseYear} • {(MediaType == "tv" ? "TV Series" : "Movie")} • ★ {Rating:F1}";
}

public class TvSeason
{
    public int SeasonNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EpisodeCount { get; set; }
    public string? PosterUrl { get; set; }
    public string? Overview { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Season {SeasonNumber}" : Name;
}

public class TvEpisode
{
    public int EpisodeNumber { get; set; }
    public int SeasonNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    public string? StillUrl { get; set; }
    public string AirDate { get; set; } = string.Empty;
    public double VoteAverage { get; set; }

    public string EpisodeCode => $"S{SeasonNumber:D2}E{EpisodeNumber:D2}";
    public string DisplayTitle => $"{EpisodeCode} — {Title}";
}

public class DirectStreamSource
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Quality { get; set; } = "Unknown";
    public string? Size { get; set; }
    public string SizeText => Size ?? string.Empty;
    public string Format { get; set; } = "Direct Stream";
    public string Url { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public bool IsDirectStream { get; set; } = true;
    public Dictionary<string, string>? Headers { get; set; }

    public string DisplayLabel
    {
        get
        {
            var label = !string.IsNullOrWhiteSpace(Quality) && Quality != "Unknown" ? $"[{Quality}] {Name}" : Name;
            if (!string.IsNullOrWhiteSpace(Size))
            {
                label += $" ({Size})";
            }
            return label;
        }
    }
}
