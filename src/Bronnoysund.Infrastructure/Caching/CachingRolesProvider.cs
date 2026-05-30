// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Dtos;
using Bronnoysund.Application.Ports;
using Bronnoysund.Application.Results;
using Bronnoysund.Domain;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bronnoysund.Infrastructure.Caching;

/// <summary>
/// Decorator around another <see cref="IRolesProvider"/> that uses HybridCache to avoid
/// unnecessary calls to Brreg. Cache key is "roles:{orgnr}", with TTL from
/// <see cref="BrregOptions.CacheTtl"/> (roles drift at roughly the same slow pace as the entity
/// record). The <see cref="CompanyRolesResult"/> union is flattened into <see cref="CachedRoles"/>
/// before serialization, since HybridCache + System.Text.Json do not serialize discriminated
/// unions polymorphically without extra config.
/// </summary>
internal sealed class CachingRolesProvider(
    IRolesProvider inner,
    HybridCache cache,
    IOptions<BrregOptions> options,
    ILogger<CachingRolesProvider> logger) : IRolesProvider
{
    public async Task<CompanyRolesResult> GetRolesAsync(OrganizationNumber org, CancellationToken ct)
    {
        var cacheKey = $"roles:{org.Value}";
        var ttl = options.Value.CacheTtl;

        logger.LogDebug("Cache lookup for {Key}", cacheKey);

        var cached = await cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var result = await inner.GetRolesAsync(org, cancel);
                return CachedRoles.From(result);
            },
            new HybridCacheEntryOptions { Expiration = ttl },
            cancellationToken: ct);

        return cached.ToResult();
    }
}

/// <summary>
/// Cache-friendly flat representation of a <see cref="CompanyRolesResult"/>.
/// Only one of the fields is non-null per instance.
/// </summary>
public sealed record CachedRoles(
    CompanyRolesResponse? Found,
    string? NotFoundOrgnr,
    string? UnavailableMessage)
{
    public static CachedRoles From(CompanyRolesResult r) => r switch
    {
        CompanyRolesResult.Found f => new CachedRoles(f.Roles, null, null),
        CompanyRolesResult.NotFound nf => new CachedRoles(null, nf.OrganizationNumber, null),
        CompanyRolesResult.Unavailable u => new CachedRoles(null, null, u.Message),
        CompanyRolesResult.InvalidInput inv => new CachedRoles(null, null, inv.Message),
        _ => throw new InvalidOperationException($"Unknown CompanyRolesResult: {r.GetType().Name}"),
    };

    public CompanyRolesResult ToResult()
    {
        if (Found is not null)
        {
            return new CompanyRolesResult.Found(Found);
        }
        if (NotFoundOrgnr is not null)
        {
            return new CompanyRolesResult.NotFound(NotFoundOrgnr);
        }
        if (UnavailableMessage is not null)
        {
            return new CompanyRolesResult.Unavailable(UnavailableMessage);
        }
        throw new InvalidOperationException("CachedRoles is empty — invalid state.");
    }
}
