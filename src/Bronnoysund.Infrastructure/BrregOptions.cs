// SPDX-License-Identifier: MIT

namespace Bronnoysund.Infrastructure;

/// <summary>
/// Configuration for the Brreg integration. Bound to the "Brreg" section in appsettings.json
/// via IOptions{T}. The default values mirror Brreg's official API.
/// </summary>
public sealed class BrregOptions
{
    public const string SectionName = "Brreg";

    public string BaseUrl { get; set; } = "https://data.brreg.no/enhetsregisteret/api/";

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// TTL for paginated name-search results. Shorter than <see cref="CacheTtl"/> because
    /// search hits drift faster than entity-level lookups, but long enough that a user
    /// paging through their results doesn't re-hit Brreg for each page.
    /// </summary>
    public TimeSpan SearchCacheTtl { get; set; } = TimeSpan.FromMinutes(5);

    public string UserAgent { get; set; } = "Bronnoysund.MVP/0.1 (+https://github.com/erlingsm/Bronnoysund.MVP)";
}
