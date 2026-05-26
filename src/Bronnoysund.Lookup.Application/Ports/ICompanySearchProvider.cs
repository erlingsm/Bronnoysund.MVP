// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

namespace Bronnoysund.Lookup.Application.Ports;

/// <summary>
/// Free-text name search against the source registry. Returns a paginated list of hits;
/// the caller is expected to follow up with a per-orgnr lookup on the chosen entry.
/// Separate from <see cref="ICompanyProvider"/> because the responses, caching strategy,
/// and consumer flow are all different.
/// </summary>
public interface ICompanySearchProvider
{
    Task<CompanySearchResult> SearchByNameAsync(string query, int maxResults, CancellationToken ct);
}

/// <summary>One row in a name-search result list.</summary>
public sealed record CompanySearchHit(
    string OrganizationNumber,
    string Name,
    string OrganizationFormCode,
    string? PostalCity);

/// <summary>
/// A page of search hits. TotalElements reflects the registry's full result count so the UI
/// can hint the user to narrow the query when only the first N of M are shown.
/// </summary>
public sealed record CompanySearchResult(
    IReadOnlyList<CompanySearchHit> Hits,
    int TotalElements);
