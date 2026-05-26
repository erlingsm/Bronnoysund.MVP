// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Application.Dtos;
using Bronnoysund.Lookup.Application.Ports;
using Bronnoysund.Lookup.Application.Results;
using Bronnoysund.Lookup.Domain;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bronnoysund.Lookup.Infrastructure.Caching;

/// <summary>
/// Decorator around another <see cref="ICompanyProvider"/> that uses HybridCache to
/// avoid unnecessary calls to Brreg. The cache key is "org:{orgnr}", with TTL from
/// <see cref="BrregOptions"/>. We wrap <see cref="CompanyLookupResult"/> in a concrete
/// record (<see cref="CachedLookup"/>) before serialization — HybridCache + System.Text.Json
/// do not support polymorphic serialization of discriminated unions without extra config.
/// </summary>
internal sealed class CachingCompanyProvider(
    ICompanyProvider inner,
    HybridCache cache,
    IOptions<BrregOptions> options,
    ILogger<CachingCompanyProvider> logger) : ICompanyProvider
{
    public async Task<CompanyLookupResult> LookupAsync(OrganizationNumber org, CancellationToken ct)
    {
        var cacheKey = $"org:{org.Value}";
        var ttl = options.Value.CacheTtl;

        logger.LogDebug("Cache lookup for {Key}", cacheKey);

        var cached = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var result = await inner.LookupAsync(org, cancel);
                return CachedLookup.From(result);
            },
            new HybridCacheEntryOptions { Expiration = ttl },
            cancellationToken: ct);

        return cached.ToResult();
    }
}

/// <summary>
/// Cache-friendly flat representation of a <see cref="CompanyLookupResult"/>.
/// Only one of the fields is non-null per instance.
/// </summary>
public sealed record CachedLookup(
    CompanyResponse? Found,
    string? NotFoundOrgnr,
    string? UnavailableMessage)
{
    public static CachedLookup From(CompanyLookupResult r) => r switch
    {
        CompanyLookupResult.Found f => new CachedLookup(f.Company, null, null),
        CompanyLookupResult.NotFound nf => new CachedLookup(null, nf.OrganizationNumber, null),
        CompanyLookupResult.Unavailable u => new CachedLookup(null, null, u.Message),
        CompanyLookupResult.InvalidInput inv => new CachedLookup(null, null, inv.Message),
        _ => throw new InvalidOperationException($"Unknown CompanyLookupResult: {r.GetType().Name}"),
    };

    public CompanyLookupResult ToResult()
    {
        if (Found is not null)
        {
            return new CompanyLookupResult.Found(Found);
        }
        if (NotFoundOrgnr is not null)
        {
            return new CompanyLookupResult.NotFound(NotFoundOrgnr);
        }
        if (UnavailableMessage is not null)
        {
            return new CompanyLookupResult.Unavailable(UnavailableMessage);
        }
        throw new InvalidOperationException("CachedLookup is empty — invalid state.");
    }
}
