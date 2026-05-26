// SPDX-License-Identifier: MIT

using Bronnoysund.Domain;
using Bronnoysund.Infrastructure.Brreg;
using Bronnoysund.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Bronnoysund.Infrastructure.Tests;

/// <summary>
/// Integration tests for BrregHttpClient that verify HTTP handling without hitting
/// the real Brreg service. WireMock.Net mocks the /enheter/{orgnr} response.
/// </summary>
[TestClass]
public class BrregHttpClientIntegrationTests : IDisposable
{
    private WireMockServer _wireMock = null!;
    private HttpClient _httpClient = null!;
    private BrregHttpClient _sut = null!;
    private static readonly OrganizationNumber Equinor = OrganizationNumber.Create("919300388");

    [TestInitialize]
    public void Setup()
    {
        _wireMock = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_wireMock.Url!) };
        _sut = new BrregHttpClient(_httpClient, NullLogger<BrregHttpClient>.Instance);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _wireMock?.Stop();
        _wireMock?.Dispose();
        GC.SuppressFinalize(this);
    }

    [TestMethod]
    public async Task Status200WithJson_ReturnsDto()
    {
        const string body = """
            {
              "organisasjonsnummer": "919300388",
              "navn": "EQUINOR ASA",
              "organisasjonsform": { "kode": "AS", "beskrivelse": "Aksjeselskap" },
              "maalform": "Bokmål"
            }
            """;
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        var dto = await _sut.GetEnhetAsync(Equinor, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Organisasjonsnummer.Should().Be("919300388");
        dto.Navn.Should().Be("EQUINOR ASA");
        dto.Organisasjonsform!.Kode.Should().Be("AS");
    }

    [TestMethod]
    public async Task Status404_ReturnsNull()
    {
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var dto = await _sut.GetEnhetAsync(Equinor, CancellationToken.None);

        dto.Should().BeNull();
    }

    [TestMethod]
    public async Task Status410Gone_ReturnsNull()
    {
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(410));

        var dto = await _sut.GetEnhetAsync(Equinor, CancellationToken.None);

        dto.Should().BeNull();
    }

    [TestMethod]
    public async Task Status500_ThrowsBrregUnavailable()
    {
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        var act = async () => await _sut.GetEnhetAsync(Equinor, CancellationToken.None);

        await act.Should().ThrowAsync<BrregUnavailableException>();
    }

    [TestMethod]
    public async Task DtoMappingToDomain_WorksForBokmal()
    {
        var dto = new BrregEnhetDto
        {
            Organisasjonsnummer = "919300388",
            Navn = "Equinor ASA",
            Organisasjonsform = new BrregOrganisasjonsformDto { Kode = "AS" },
            Maalform = "Bokmål",
        };

        var company = dto.ToDomain();

        company.Should().NotBeNull();
        company!.OrganizationNumber.Value.Should().Be("919300388");
        company.Name.Should().Be("Equinor ASA");
        company.OrganizationFormCode.Should().Be("AS");
        company.LanguageForm.Should().Be(LanguageForm.Bokmål);
    }
}
