// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Application.Results;
using Bronnoysund.Lookup.Domain;
using Bronnoysund.Lookup.Infrastructure.Brreg;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Bronnoysund.Lookup.Infrastructure.Tests;

/// <summary>
/// Verifies that BrregCompanyProvider projects every open Brreg entity field we care about
/// into CompanyResponse — addresses, contact info, industry, employees, sector, dates, registry
/// flags, bankruptcy. The integration test above covers HTTP status handling; this one isolates
/// the mapping logic so a Brreg-side schema change shows up here first.
/// </summary>
public class BrregCompanyProviderMappingTests : IDisposable
{
    private readonly WireMockServer _wireMock;
    private readonly HttpClient _httpClient;
    private readonly BrregCompanyProvider _sut;
    private static readonly OrganizationNumber Riksrevisjonen = OrganizationNumber.Create("974760843");

    public BrregCompanyProviderMappingTests()
    {
        _wireMock = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_wireMock.Url!) };
        var brreg = new BrregHttpClient(_httpClient, NullLogger<BrregHttpClient>.Instance);
        _sut = new BrregCompanyProvider(brreg, NullLogger<BrregCompanyProvider>.Instance);
    }

    [Fact]
    public async Task FullPayload_MapsEveryOpenField()
    {
        const string body = """
            {
              "organisasjonsnummer": "974760843",
              "navn": "RIKSREVISJONEN",
              "organisasjonsform": { "kode": "ORGL", "beskrivelse": "Organisasjonsledd" },
              "maalform": "Bokmål",
              "konkurs": false,
              "konkursdato": null,
              "hjemmeside": "www.riksrevisjonen.no/",
              "epostadresse": "postmottak@riksrevisjonen.no",
              "telefon": "22 24 10 00",
              "mobil": "99 99 99 99",
              "forretningsadresse": {
                "adresse": ["Storgata 16"],
                "postnummer": "0184",
                "poststed": "OSLO",
                "kommune": "OSLO",
                "kommunenummer": "0301",
                "land": "Norge",
                "landkode": "NO"
              },
              "postadresse": {
                "adresse": ["Postboks 6835", "St. Olavs plass"],
                "postnummer": "0130",
                "poststed": "OSLO"
              },
              "naeringskode1": { "kode": "84.110", "beskrivelse": "Generell offentlig administrasjon" },
              "antallAnsatte": 445,
              "harRegistrertAntallAnsatte": true,
              "institusjonellSektorkode": { "kode": "6100", "beskrivelse": "Statsforvaltningen" },
              "stiftelsesdato": "1819-04-12",
              "registreringsdatoEnhetsregisteret": "1995-08-09",
              "registrertIMvaregisteret": false,
              "registrertIForetaksregisteret": false
            }
            """;
        _wireMock.Given(Request.Create().WithPath("/enheter/974760843").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

        var result = await _sut.LookupAsync(Riksrevisjonen, CancellationToken.None);

        var found = result.Should().BeOfType<CompanyLookupResult.Found>().Subject;
        var company = found.Company;

        company.OrganizationNumber.Should().Be("974760843");
        company.OrganizationName.Should().Be("RIKSREVISJONEN");
        company.CompanyType.Should().Be("ORGL");
        company.LanguageForm.Should().Be("Bokmål");
        company.Website.Should().Be("www.riksrevisjonen.no/");
        company.Email.Should().Be("postmottak@riksrevisjonen.no");
        company.Phone.Should().Be("22 24 10 00");
        company.MobilePhone.Should().Be("99 99 99 99");

        company.BusinessAddress.Should().NotBeNull();
        company.BusinessAddress!.StreetAddress.Should().Be("Storgata 16");
        company.BusinessAddress.PostalCode.Should().Be("0184");
        company.BusinessAddress.City.Should().Be("OSLO");
        company.BusinessAddress.Municipality.Should().Be("OSLO");
        company.BusinessAddress.Country.Should().Be("Norge");

        company.PostalAddress.Should().NotBeNull();
        company.PostalAddress!.StreetAddress.Should().Be("Postboks 6835 St. Olavs plass"); // joined with space

        company.PrimaryIndustry.Should().NotBeNull();
        company.PrimaryIndustry!.Code.Should().Be("84.110");
        company.PrimaryIndustry.Description.Should().Be("Generell offentlig administrasjon");

        company.EmployeeCount.Should().Be(445);
        company.SectorCode.Should().Be("6100");
        company.SectorDescription.Should().Be("Statsforvaltningen");
        company.FoundingDate.Should().Be(new DateOnly(1819, 4, 12));
        company.RegisteredDate.Should().Be(new DateOnly(1995, 8, 9));
        company.RegisteredInVatRegistry.Should().BeFalse();
        company.RegisteredInBusinessRegistry.Should().BeFalse();
        company.IsBankrupt.Should().BeFalse();
        company.BankruptcyDate.Should().BeNull();
        company.DeletedDate.Should().BeNull();
    }

    [Fact]
    public async Task MinimalPayload_AllOptionalFieldsAreNull()
    {
        // What Brreg returns for a brand-new entity: just core fields, optional ones absent.
        const string body = """
            {
              "organisasjonsnummer": "919300388",
              "navn": "EQUINOR ASA",
              "organisasjonsform": { "kode": "AS" },
              "maalform": "Bokmål",
              "konkurs": false
            }
            """;
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

        var result = await _sut.LookupAsync(OrganizationNumber.Create("919300388"), CancellationToken.None);

        var company = result.Should().BeOfType<CompanyLookupResult.Found>().Subject.Company;
        company.Website.Should().BeNull();
        company.BusinessAddress.Should().BeNull();
        company.PostalAddress.Should().BeNull();
        company.PrimaryIndustry.Should().BeNull();
        company.EmployeeCount.Should().BeNull();
        company.IsBankrupt.Should().BeFalse();
    }

    [Fact]
    public async Task EmployeeCount_IsNullWhenNotRegistered()
    {
        // Brreg returns antallAnsatte=0 with harRegistrertAntallAnsatte=false for entities that
        // have never reported employee count. Showing "0 employees" would mislead the user;
        // we surface null instead.
        const string body = """
            {
              "organisasjonsnummer": "919300388",
              "navn": "EQUINOR ASA",
              "organisasjonsform": { "kode": "AS" },
              "maalform": "Bokmål",
              "konkurs": false,
              "antallAnsatte": 0,
              "harRegistrertAntallAnsatte": false
            }
            """;
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

        var result = await _sut.LookupAsync(OrganizationNumber.Create("919300388"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.Found>().Which.Company.EmployeeCount.Should().BeNull();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _wireMock.Stop();
        _wireMock.Dispose();
        GC.SuppressFinalize(this);
    }
}
