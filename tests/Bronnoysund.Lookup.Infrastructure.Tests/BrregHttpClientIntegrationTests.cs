// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using System.Net;
using Bronnoysund.Lookup.Domain;
using Bronnoysund.Lookup.Infrastructure.Brreg;
using Bronnoysund.Lookup.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Bronnoysund.Lookup.Infrastructure.Tests;

/// <summary>
/// Integration tests for BrregHttpClient that verify HTTP handling without hitting
/// the real Brreg service. WireMock.Net mocks the /enheter/{orgnr} response.
/// </summary>
public class BrregHttpClientIntegrationTests : IDisposable
{
    private readonly WireMockServer _wireMock;
    private readonly HttpClient _httpClient;
    private readonly BrregHttpClient _sut;
    private static readonly OrganizationNumber Equinor = OrganizationNumber.Create("919300388");

    public BrregHttpClientIntegrationTests()
    {
        _wireMock = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_wireMock.Url!) };
        _sut = new BrregHttpClient(_httpClient, NullLogger<BrregHttpClient>.Instance);
    }

    [Fact]
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

    [Fact]
    public async Task Status404_ReturnsNull()
    {
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var dto = await _sut.GetEnhetAsync(Equinor, CancellationToken.None);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task Status410Gone_ReturnsNull()
    {
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(410));

        var dto = await _sut.GetEnhetAsync(Equinor, CancellationToken.None);

        dto.Should().BeNull();
    }

    [Fact]
    public async Task Status500_ThrowsBrregUnavailable()
    {
        _wireMock.Given(Request.Create().WithPath("/enheter/919300388").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        var act = async () => await _sut.GetEnhetAsync(Equinor, CancellationToken.None);

        await act.Should().ThrowAsync<BrregUnavailableException>();
    }

    [Fact]
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

    public void Dispose()
    {
        _httpClient.Dispose();
        _wireMock.Stop();
        _wireMock.Dispose();
        GC.SuppressFinalize(this);
    }
}
