// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Application.Ports;
using Bronnoysund.Lookup.Application.Results;
using Bronnoysund.Lookup.Domain;
using Microsoft.Extensions.Logging;

namespace Bronnoysund.Lookup.Application.UseCases.LookupCompany;

/// <summary>
/// Use case handler for organization-number lookups. Validates the input, builds an
/// <see cref="OrganizationNumber"/>, and delegates to <see cref="ICompanyProvider"/>. Returns a type-safe
/// <see cref="CompanyLookupResult"/> — no exceptions for business outcomes.
/// </summary>
public sealed class LookupCompanyHandler(
    ICompanyProvider provider,
    ILogger<LookupCompanyHandler> logger)
{
    public async Task<CompanyLookupResult> HandleAsync(LookupCompanyQuery query, CancellationToken ct)
    {
        if (!OrganizationNumber.TryCreate(query.OrganizationNumberInput, out var orgNumber, out var error))
        {
            logger.LogInformation("Validation failed for orgnr input '{Input}': {Error}",
                query.OrganizationNumberInput, error);
            return new CompanyLookupResult.InvalidInput(error ?? "Invalid organization number.");
        }

        logger.LogInformation("Looking up orgnr {OrgNumber}", orgNumber.Value);
        return await provider.LookupAsync(orgNumber, ct);
    }
}
