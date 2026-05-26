// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

namespace Bronnoysund.Lookup.Infrastructure;

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

    public string UserAgent { get; set; } = "Bronnoysund.Lookup/0.1 (+https://github.com/erlingsm/Bronnoysund.Lookup)";
}
