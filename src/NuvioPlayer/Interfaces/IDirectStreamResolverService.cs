namespace NuvioPlayer.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuvioPlayer.Models;

public interface IDirectStreamResolverService
{
    Task<List<DirectStreamSource>> ResolveStreamsAsync(
        int tmdbId,
        string mediaType,
        int? season = null,
        int? episode = null,
        CancellationToken cancellationToken = default);

    string GetEmbedFallbackUrl(
        int tmdbId,
        string mediaType,
        int? season = null,
        int? episode = null,
        string provider = "vidlink");
}
