// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Results;
using Bronnoysund.Domain;
using Bronnoysund.Infrastructure.Brreg;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Bronnoysund.Infrastructure.Tests;

/// <summary>
/// Verifies that BrregCompanyProvider projects every open Brreg entity field we care about
/// into CompanyResponse — addresses, contact info, industry, employees, sector, dates, registry
/// flags, bankruptcy. The integration test above covers HTTP status handling; this one isolates
/// the mapping logic so a Brreg-side schema change shows up here first.
/// </summary>
[TestClass]
public class BrregCompanyProviderMappingTests : IDisposable
{
    private WireMockServer _wireMock = null!;
    private HttpClient _httpClient = null!;
    private BrregCompanyProvider _sut = null!;
    private static readonly OrganizationNumber Riksrevisjonen = OrganizationNumber.Create("974760843");

    [TestInitialize]
    public void Setup()
    {
        _wireMock = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_wireMock.Url!) };
        var brreg = new BrregHttpClient(_httpClient, NullLogger<BrregHttpClient>.Instance);
        _sut = new BrregCompanyProvider(brreg, NullLogger<BrregCompanyProvider>.Instance);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _wireMock?.Stop();
        _wireMock?.Dispose();
        GC.SuppressFinalize(this);
    }

    [TestMethod]
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

        company.Details.Should().NotBeNull();
        var details = company.Details!;
        details.Website.Should().Be("www.riksrevisjonen.no/");
        details.Email.Should().Be("postmottak@riksrevisjonen.no");
        details.Phone.Should().Be("22 24 10 00");
        details.MobilePhone.Should().Be("99 99 99 99");

        details.BusinessAddress.Should().NotBeNull();
        details.BusinessAddress!.StreetAddress.Should().Be("Storgata 16");
        details.BusinessAddress.PostalCode.Should().Be("0184");
        details.BusinessAddress.City.Should().Be("OSLO");
        details.BusinessAddress.Municipality.Should().Be("OSLO");
        details.BusinessAddress.Country.Should().Be("Norge");

        details.PostalAddress.Should().NotBeNull();
        details.PostalAddress!.StreetAddress.Should().Be("Postboks 6835 St. Olavs plass"); // joined with space

        details.PrimaryIndustry.Should().NotBeNull();
        details.PrimaryIndustry!.Code.Should().Be("84.110");
        details.PrimaryIndustry.Description.Should().Be("Generell offentlig administrasjon");

        details.EmployeeCount.Should().Be(445);
        details.SectorCode.Should().Be("6100");
        details.SectorDescription.Should().Be("Statsforvaltningen");
        details.FoundingDate.Should().Be(new DateOnly(1819, 4, 12));
        details.RegisteredDate.Should().Be(new DateOnly(1995, 8, 9));
        details.RegisteredInVatRegistry.Should().BeFalse();
        details.RegisteredInBusinessRegistry.Should().BeFalse();
        details.IsBankrupt.Should().BeFalse();
        details.BankruptcyDate.Should().BeNull();
        details.DeletedDate.Should().BeNull();
    }

    [TestMethod]
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
        company.Details.Should().NotBeNull();
        var details = company.Details!;
        details.Website.Should().BeNull();
        details.BusinessAddress.Should().BeNull();
        details.PostalAddress.Should().BeNull();
        details.PrimaryIndustry.Should().BeNull();
        details.EmployeeCount.Should().BeNull();
        details.IsBankrupt.Should().BeFalse();
    }

    [TestMethod]
    public async Task DtoMissingCriticalFields_ReturnsUnavailable()
    {
        // Brreg responded 200 but the payload is missing essential fields (here: `navn`).
        // BrregEnhetDto.ToDomain returns null in this case, and the provider surfaces it as
        // Unavailable rather than Found-with-empty-data or a thrown exception. Catches the
        // class of schema-drift bugs where Brreg silently drops a field we depend on.
        const string body = """
            {
              "organisasjonsnummer": "919300388",
              "organisasjonsform": { "kode": "AS" }
            }
            """;
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

        var result = await _sut.LookupAsync(OrganizationNumber.Create("919300388"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.Unavailable>()
            .Which.Message.Should().Contain("unexpected");
    }

    [TestMethod]
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

        result.Should().BeOfType<CompanyLookupResult.Found>().Which.Company.Details!.EmployeeCount.Should().BeNull();
    }
}
