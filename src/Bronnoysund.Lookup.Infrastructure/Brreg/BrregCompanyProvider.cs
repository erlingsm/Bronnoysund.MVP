// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using System.Globalization;
using Bronnoysund.Lookup.Application.Dtos;
using Bronnoysund.Lookup.Application.Ports;
using Bronnoysund.Lookup.Application.Results;
using Bronnoysund.Lookup.Domain;
using Bronnoysund.Lookup.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bronnoysund.Lookup.Infrastructure.Brreg;

/// <summary>
/// Adapter that implements <see cref="ICompanyProvider"/> via the Brreg Enhetsregisteret.
/// Maps the Brreg DTO to our domain entity and to the English-field <see cref="CompanyResponse"/>.
/// The mapping projects every open field Brreg exposes on the entity payload — contact info,
/// address, industry, employees, lifecycle dates — without any additional API calls.
/// </summary>
internal sealed class BrregCompanyProvider(
    BrregHttpClient httpClient,
    ILogger<BrregCompanyProvider> logger) : ICompanyProvider
{
    public async Task<CompanyLookupResult> LookupAsync(OrganizationNumber org, CancellationToken ct)
    {
        try
        {
            var dto = await httpClient.GetEnhetAsync(org, ct);
            if (dto is null)
            {
                return new CompanyLookupResult.NotFound(org.Value);
            }

            var domain = dto.ToDomain();
            if (domain is null)
            {
                logger.LogWarning("Brreg returned a response for {OrgNumber} but mapping did not produce a valid Company", org.Value);
                return new CompanyLookupResult.Unavailable(
                    "Brreg returned an unexpected response structure.");
            }

            return new CompanyLookupResult.Found(MapToResponse(dto, domain));
        }
        catch (BrregUnavailableException ex)
        {
            logger.LogWarning(ex, "Brreg unavailable for {OrgNumber}", org.Value);
            return new CompanyLookupResult.Unavailable(ex.Message);
        }
    }

    private static CompanyResponse MapToResponse(BrregEnhetDto dto, Company domain) => new(
        OrganizationNumber: domain.OrganizationNumber.Value,
        OrganizationName: domain.Name,
        CompanyType: domain.OrganizationFormCode,
        LanguageForm: domain.LanguageForm.ToString(),
        Website: NullIfEmpty(dto.Hjemmeside),
        Email: NullIfEmpty(dto.Epostadresse),
        Phone: NullIfEmpty(dto.Telefon),
        MobilePhone: NullIfEmpty(dto.Mobil),
        BusinessAddress: MapAddress(dto.Forretningsadresse),
        PostalAddress: MapAddress(dto.Postadresse),
        PrimaryIndustry: MapIndustry(dto.Naeringskode1),
        EmployeeCount: dto.HarRegistrertAntallAnsatte == true ? dto.AntallAnsatte : null,
        SectorCode: dto.InstitusjonellSektorkode?.Kode,
        SectorDescription: dto.InstitusjonellSektorkode?.Beskrivelse,
        FoundingDate: ParseDate(dto.Stiftelsesdato),
        RegisteredDate: ParseDate(dto.RegistreringsdatoEnhetsregisteret),
        RegisteredInVatRegistry: dto.RegistrertIMvaregisteret,
        RegisteredInBusinessRegistry: dto.RegistrertIForetaksregisteret,
        IsBankrupt: dto.Konkurs,
        BankruptcyDate: ParseDate(dto.Konkursdato),
        DeletedDate: ParseDate(dto.Slettedato));

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static PostalAddress? MapAddress(BrregFullAdresseDto? a)
    {
        if (a is null)
        {
            return null;
        }
        return new PostalAddress(
            StreetAddress: a.StreetLine(),
            PostalCode: NullIfEmpty(a.Postnummer),
            City: NullIfEmpty(a.Poststed),
            Municipality: NullIfEmpty(a.Kommune),
            Country: NullIfEmpty(a.Land));
    }

    private static IndustryCode? MapIndustry(BrregKodeDto? code) =>
        code is null || string.IsNullOrWhiteSpace(code.Kode)
            ? null
            : new IndustryCode(code.Kode, code.Beskrivelse ?? string.Empty);

    private static DateOnly? ParseDate(string? iso) =>
        string.IsNullOrWhiteSpace(iso)
            ? null
            : DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : null;
}
