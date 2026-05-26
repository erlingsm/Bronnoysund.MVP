// SPDX-License-Identifier: MIT

namespace Bronnoysund.Application.Ports;

/// <summary>
/// Free-text name search against the source registry. Returns a paginated list of hits;
/// the caller is expected to follow up with a per-orgnr lookup on the chosen entry.
/// Separate from <see cref="ICompanyProvider"/> because the responses, caching strategy,
/// and consumer flow are all different.
/// </summary>
public interface ICompanySearchProvider
{
    Task<CompanySearchResult> SearchByNameAsync(string query, int pageSize, int page, CancellationToken ct);
}

/// <summary>One row in a name-search result list.</summary>
public sealed record CompanySearchHit(
    string OrganizationNumber,
    string Name,
    string OrganizationFormCode,
    string? PostalCity);

/// <summary>
/// A page of search hits. TotalElements reflects the registry's full result count;
/// Page / TotalPages / PageSize echo the page metadata Brreg returns so the UI can render
/// a pagination control and the cache layer can key on (query, pageSize, page).
/// </summary>
public sealed record CompanySearchResult(
    IReadOnlyList<CompanySearchHit> Hits,
    int TotalElements,
    int Page = 0,
    int TotalPages = 1,
    int PageSize = 25);
