// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Ports;
using Bronnoysund.Application.Results;
using Bronnoysund.Domain;
using Microsoft.Extensions.Logging;

namespace Bronnoysund.Application.UseCases.LookupCompanyRoles;

/// <summary>
/// Use case handler for the roles of an entity. Validates the organization number and delegates
/// to <see cref="IRolesProvider"/>. Returns a type-safe <see cref="CompanyRolesResult"/> — no
/// exceptions for business outcomes.
/// </summary>
public sealed class LookupCompanyRolesHandler(
    IRolesProvider provider,
    ILogger<LookupCompanyRolesHandler> logger)
{
    public async Task<CompanyRolesResult> HandleAsync(LookupCompanyRolesQuery query, CancellationToken ct)
    {
        if (!OrganizationNumber.TryCreate(query.OrganizationNumberInput, out var orgNumber, out var error))
        {
            logger.LogInformation("Validation failed for roles orgnr input '{Input}': {Error}",
                query.OrganizationNumberInput, error);
            return new CompanyRolesResult.InvalidInput(error ?? "Invalid organization number.");
        }

        logger.LogInformation("Looking up roles for orgnr {OrgNumber}", orgNumber.Value);
        var result = await provider.GetRolesAsync(orgNumber, ct);
        logger.LogInformation(
            "Roles lookup for {OrgNumber} completed with outcome {Outcome}",
            orgNumber.Value, result.GetType().Name);
        return result;
    }
}
