// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Dtos;
using Bronnoysund.Application.Ports;
using Bronnoysund.Application.Results;
using Bronnoysund.Domain;
using Bronnoysund.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bronnoysund.Infrastructure.Brreg;

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
                return new CompanyLookupResult.Unavailable("Brreg returned an unexpected response structure.");
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
        Details: BuildDetails(dto));

    private static CompanyDetails BuildDetails(BrregEnhetDto dto) => new(
        Website: BrregParse.NullIfEmpty(dto.Hjemmeside),
        Email: BrregParse.NullIfEmpty(dto.Epostadresse),
        Phone: BrregParse.NullIfEmpty(dto.Telefon),
        MobilePhone: BrregParse.NullIfEmpty(dto.Mobil),
        BusinessAddress: MapAddress(dto.Forretningsadresse),
        PostalAddress: MapAddress(dto.Postadresse),
        PrimaryIndustry: MapIndustry(dto.Naeringskode1),
        EmployeeCount: dto.HarRegistrertAntallAnsatte == true ? dto.AntallAnsatte : null,
        SectorCode: dto.InstitusjonellSektorkode?.Kode,
        SectorDescription: dto.InstitusjonellSektorkode?.Beskrivelse,
        FoundingDate: BrregParse.Date(dto.Stiftelsesdato),
        RegisteredDate: BrregParse.Date(dto.RegistreringsdatoEnhetsregisteret),
        RegisteredInVatRegistry: dto.RegistrertIMvaregisteret,
        RegisteredInBusinessRegistry: dto.RegistrertIForetaksregisteret,
        IsBankrupt: dto.Konkurs,
        BankruptcyDate: BrregParse.Date(dto.Konkursdato),
        DeletedDate: BrregParse.Date(dto.Slettedato));

    private static PostalAddress? MapAddress(BrregFullAdresseDto? a)
    {
        if (a is null)
        {
            return null;
        }
        return new PostalAddress(
            StreetAddress: a.StreetLine(),
            PostalCode: BrregParse.NullIfEmpty(a.Postnummer),
            City: BrregParse.NullIfEmpty(a.Poststed),
            Municipality: BrregParse.NullIfEmpty(a.Kommune),
            Country: BrregParse.NullIfEmpty(a.Land));
    }

    private static IndustryCode? MapIndustry(BrregKodeDto? code) =>
        code is null || string.IsNullOrWhiteSpace(code.Kode)
            ? null
            : new IndustryCode(code.Kode, code.Beskrivelse ?? string.Empty);
}
