// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Ports;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bronnoysund.Infrastructure.Caching;

/// <summary>
/// Decorator around <see cref="ICompanySearchProvider"/> that uses HybridCache to keep paged
/// name-search results warm while the user pages through them. The cache key is
/// "search:{query}:{pageSize}:{page}" with the query lower-cased so case variations of the
/// same search re-use one entry. TTL comes from <see cref="BrregOptions.SearchCacheTtl"/>
/// (default 5 minutes) — long enough to cover normal paging interaction, short enough that
/// the user gets a fresh view if they come back later.
/// </summary>
internal sealed class CachingCompanySearchProvider(
    ICompanySearchProvider inner,
    HybridCache cache,
    IOptions<BrregOptions> options,
    ILogger<CachingCompanySearchProvider> logger) : ICompanySearchProvider
{
    public async Task<CompanySearchResult> SearchByNameAsync(string query, int pageSize, int page, CancellationToken ct)
    {
        var cacheKey = $"search:{query.ToLowerInvariant()}:{pageSize}:{page}";
        var ttl = options.Value.SearchCacheTtl;

        logger.LogDebug("Cache lookup for {Key}", cacheKey);

        return await cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await inner.SearchByNameAsync(query, pageSize, page, cancel),
            new HybridCacheEntryOptions { Expiration = ttl },
            cancellationToken: ct);
    }
}
