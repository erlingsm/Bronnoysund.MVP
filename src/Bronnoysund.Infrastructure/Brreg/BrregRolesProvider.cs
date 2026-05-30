// SPDX-License-Identifier: MIT

using System.Globalization;
using Bronnoysund.Application.Dtos;
using Bronnoysund.Application.Ports;
using Bronnoysund.Application.Results;
using Bronnoysund.Domain;
using Bronnoysund.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bronnoysund.Infrastructure.Brreg;

/// <summary>
/// Adapter that implements <see cref="IRolesProvider"/> via the open Brreg
/// <c>/enheter/{orgnr}/roller</c> endpoint. Maps the grouped Brreg payload into
/// <see cref="CompanyRolesResponse"/>, keeping Brreg's role groups intact and dropping roles
/// that are resigned (<c>fratraadt</c>) or deregistered (<c>avregistrert</c>). Only open data —
/// names and birth dates — is projected; fødselsnummer is never read or exposed.
/// </summary>
internal sealed class BrregRolesProvider(
    BrregHttpClient httpClient,
    ILogger<BrregRolesProvider> logger) : IRolesProvider
{
    public async Task<CompanyRolesResult> GetRolesAsync(OrganizationNumber org, CancellationToken ct)
    {
        try
        {
            var dto = await httpClient.GetRollerAsync(org, ct);
            if (dto is null)
            {
                return new CompanyRolesResult.NotFound(org.Value);
            }

            return new CompanyRolesResult.Found(MapToResponse(org, dto));
        }
        catch (BrregUnavailableException ex)
        {
            logger.LogWarning(ex, "Brreg roles unavailable for {OrgNumber}", org.Value);
            return new CompanyRolesResult.Unavailable(ex.Message);
        }
    }

    private static CompanyRolesResponse MapToResponse(OrganizationNumber org, BrregRollerDto dto)
    {
        var groups = new List<RoleGroup>();

        foreach (var group in dto.Rollegrupper ?? [])
        {
            var holders = new List<RoleHolder>();

            foreach (var role in (group.Roller ?? [])
                .OrderBy(r => r.Rekkefolge ?? int.MaxValue))
            {
                // Skip roles no longer in effect. Honour both the legacy `fratraadt` flag and
                // the newer `avregistrert` one during the registry's transition window.
                if (role.Fratraadt == true || role.Avregistrert == true)
                {
                    continue;
                }

                var roleTypeCode = role.Type?.Kode ?? string.Empty;
                var roleTypeDescription = role.Type?.Beskrivelse ?? role.Type?.Kode ?? "Ukjent";
                var electedBy = NullIfEmpty(role.ValgtAv?.Beskrivelse);

                if (role.Person is { } person)
                {
                    holders.Add(new RoleHolder(
                        RoleTypeCode: roleTypeCode,
                        RoleTypeDescription: roleTypeDescription,
                        Name: person.Navn?.Full() ?? "(uten navn)",
                        OrganizationNumber: null,
                        DateOfBirth: ParseDate(person.Fodselsdato),
                        IsDeceased: person.ErDoed == true,
                        ElectedBy: electedBy));
                }
                else if (role.Enhet is { } enhet)
                {
                    holders.Add(new RoleHolder(
                        RoleTypeCode: roleTypeCode,
                        RoleTypeDescription: roleTypeDescription,
                        Name: enhet.NameLine() ?? "(uten navn)",
                        OrganizationNumber: NullIfEmpty(enhet.Organisasjonsnummer),
                        DateOfBirth: null,
                        IsDeceased: false,
                        ElectedBy: electedBy));
                }
            }

            // Drop a group that has no active roles left after filtering.
            if (holders.Count > 0)
            {
                groups.Add(new RoleGroup(
                    TypeCode: group.Type?.Kode ?? string.Empty,
                    TypeDescription: group.Type?.Beskrivelse ?? group.Type?.Kode ?? "Ukjent",
                    Roles: holders));
            }
        }

        return new CompanyRolesResponse(org.Value, groups);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static DateOnly? ParseDate(string? iso) =>
        string.IsNullOrWhiteSpace(iso)
            ? null
            : DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : null;
}
