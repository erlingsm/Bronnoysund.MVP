// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Ports;
using Bronnoysund.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bronnoysund.Infrastructure.Brreg;

/// <summary>
/// ICompanySearchProvider adapter for Brreg's open /enheter?navn= endpoint. Returns one page of
/// hits plus the registry's page metadata (totalElements, totalPages, current page, page size)
/// so the UI can render pagination and the caching decorator can key on (query, pageSize, page).
/// </summary>
internal sealed class BrregCompanySearchProvider(
    BrregHttpClient http,
    ILogger<BrregCompanySearchProvider> logger) : ICompanySearchProvider
{
    public async Task<CompanySearchResult> SearchByNameAsync(string query, int pageSize, int page, CancellationToken ct)
    {
        try
        {
            var dto = await http.SearchEnheterByNameAsync(query, pageSize, page, ct);
            if (dto is null)
            {
                return new CompanySearchResult([], 0, page, 0, pageSize);
            }

            var hits = (dto.Embedded?.Enheter ?? [])
                .Where(e => !string.IsNullOrWhiteSpace(e.Organisasjonsnummer))
                .Select(e => new CompanySearchHit(
                    OrganizationNumber: e.Organisasjonsnummer!,
                    Name: e.Navn ?? string.Empty,
                    OrganizationFormCode: e.Organisasjonsform?.Kode ?? string.Empty,
                    PostalCity: e.Postadresse?.Poststed))
                .ToList();

            return new CompanySearchResult(
                Hits: hits,
                TotalElements: dto.Page?.TotalElements ?? hits.Count,
                Page: dto.Page?.Number ?? page,
                TotalPages: dto.Page?.TotalPages ?? (hits.Count > 0 ? 1 : 0),
                PageSize: dto.Page?.Size ?? pageSize);
        }
        catch (BrregUnavailableException ex)
        {
            logger.LogWarning(ex, "Brreg name search unavailable for query '{Query}' (page {Page})", query, page);
            throw;
        }
    }
}
