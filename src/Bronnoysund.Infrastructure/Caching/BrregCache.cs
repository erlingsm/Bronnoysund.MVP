// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Caching.Hybrid;

namespace Bronnoysund.Infrastructure.Caching;

/// <summary>
/// Shared read-through caching for the Brreg provider decorators. Each lookup-style provider
/// returns a discriminated-union result that is flattened to a cache-friendly record
/// (<c>TCached</c>) for serialization, since HybridCache + System.Text.Json do not serialize
/// unions polymorphically. This helper centralises the GetOrCreateAsync mechanics and the
/// negative-result eviction policy so every provider shares one implementation.
/// </summary>
internal static class BrregCache
{
    /// <summary>
    /// Read-through cache for a union <paramref name="factory"/>. The result is stored for
    /// <paramref name="ttl"/>, then — if <paramref name="retain"/> returns false (e.g. a transient
    /// <c>Unavailable</c>) — evicted immediately so a technical outage never occupies the slot for
    /// the full positive TTL. The Polly resilience pipeline (circuit breaker) guards Brreg against
    /// the repeat traffic that not caching negatives implies.
    /// </summary>
    public static async Task<TResult> GetOrCreateUnionAsync<TResult, TCached>(
        this HybridCache cache,
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<TResult>> factory,
        Func<TResult, TCached> toCached,
        Func<TCached, TResult> toResult,
        Func<TResult, bool> retain,
        CancellationToken ct)
    {
        var cached = await cache.GetOrCreateAsync(
            key,
            async cancel => toCached(await factory(cancel)),
            new HybridCacheEntryOptions { Expiration = ttl },
            cancellationToken: ct);

        var result = toResult(cached);
        if (!retain(result))
        {
            await cache.RemoveAsync(key, ct);
        }
        return result;
    }
}
