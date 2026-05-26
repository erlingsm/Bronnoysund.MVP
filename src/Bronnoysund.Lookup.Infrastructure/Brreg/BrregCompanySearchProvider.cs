// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Application.Ports;
using Bronnoysund.Lookup.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bronnoysund.Lookup.Infrastructure.Brreg;

/// <summary>
/// ICompanySearchProvider adapter for Brreg's open /enheter?navn= endpoint. Returns the first
/// page (size capped by the caller) plus the total result count so the UI can prompt the user
/// to narrow the query when results are truncated. Brreg pagination beyond page 0 is not used
/// here — if a user needs to dig deeper than 100 hits the right answer is a more specific query,
/// not paging.
/// </summary>
internal sealed class BrregCompanySearchProvider(
    BrregHttpClient http,
    ILogger<BrregCompanySearchProvider> logger) : ICompanySearchProvider
{
    public async Task<CompanySearchResult> SearchByNameAsync(string query, int maxResults, CancellationToken ct)
    {
        try
        {
            var dto = await http.SearchEnheterByNameAsync(query, maxResults, ct);
            if (dto is null)
            {
                return new CompanySearchResult([], 0);
            }

            var hits = (dto.Embedded?.Enheter ?? [])
                .Where(e => !string.IsNullOrWhiteSpace(e.Organisasjonsnummer))
                .Select(e => new CompanySearchHit(
                    OrganizationNumber: e.Organisasjonsnummer!,
                    Name: e.Navn ?? string.Empty,
                    OrganizationFormCode: e.Organisasjonsform?.Kode ?? string.Empty,
                    PostalCity: e.Postadresse?.Poststed))
                .ToList();

            return new CompanySearchResult(hits, dto.Page?.TotalElements ?? hits.Count);
        }
        catch (BrregUnavailableException ex)
        {
            logger.LogWarning(ex, "Brreg name search unavailable for query '{Query}'", query);
            throw;
        }
    }
}
