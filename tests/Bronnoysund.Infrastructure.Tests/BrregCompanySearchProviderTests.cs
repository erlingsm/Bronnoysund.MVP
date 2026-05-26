// SPDX-License-Identifier: MIT

using Bronnoysund.Infrastructure.Brreg;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Bronnoysund.Infrastructure.Tests;

[TestClass]
public class BrregCompanySearchProviderTests : IDisposable
{
    private WireMockServer _wireMock = null!;
    private HttpClient _httpClient = null!;
    private BrregCompanySearchProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _wireMock = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_wireMock.Url!) };
        var brreg = new BrregHttpClient(_httpClient, NullLogger<BrregHttpClient>.Instance);
        _sut = new BrregCompanySearchProvider(brreg, NullLogger<BrregCompanySearchProvider>.Instance);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _wireMock?.Stop();
        _wireMock?.Dispose();
        GC.SuppressFinalize(this);
    }

    [TestMethod]
    public async Task SearchByNameAsync_MapsEmbeddedHits()
    {
        const string body = """
            {
              "_embedded": {
                "enheter": [
                  {
                    "organisasjonsnummer": "971032081",
                    "navn": "STATENS VEGVESEN",
                    "organisasjonsform": { "kode": "ORGL" },
                    "postadresse": { "poststed": "LILLEHAMMER" }
                  },
                  {
                    "organisasjonsnummer": "987659025",
                    "navn": "TEKNA STATENS VEGVESEN",
                    "organisasjonsform": { "kode": "FLI" },
                    "postadresse": { "poststed": "OSLO" }
                  }
                ]
              },
              "page": { "totalElements": 142, "totalPages": 8, "number": 0, "size": 20 }
            }
            """;
        _wireMock.Given(Request.Create().WithPath("/enheter").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

        var result = await _sut.SearchByNameAsync("Statens vegvesen", 20, 0, CancellationToken.None);

        result.Hits.Should().HaveCount(2);
        result.Hits[0].OrganizationNumber.Should().Be("971032081");
        result.Hits[0].Name.Should().Be("STATENS VEGVESEN");
        result.Hits[0].OrganizationFormCode.Should().Be("ORGL");
        result.Hits[0].PostalCity.Should().Be("LILLEHAMMER");
        result.TotalElements.Should().Be(142);
    }

    [TestMethod]
    public async Task SearchByNameAsync_EmptyEmbedded_ReturnsEmptyHits()
    {
        const string body = """{"_embedded": {"enheter": []}, "page": {"totalElements": 0}}""";
        _wireMock.Given(Request.Create().WithPath("/enheter").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

        var result = await _sut.SearchByNameAsync("Bedrift som ikke finnes", 20, 0, CancellationToken.None);

        result.Hits.Should().BeEmpty();
        result.TotalElements.Should().Be(0);
    }

    [TestMethod]
    public async Task SearchByNameAsync_HandlesNorwegianCharactersInQuery()
    {
        // Verify the provider URL-encodes Norwegian characters; without this Brreg would
        // get a mangled query and either return wrong results or 400.
        const string body = """
            {
              "_embedded": { "enheter": [{"organisasjonsnummer": "971491787", "navn": "RØA ALLIANSEIDRETTSLAG", "organisasjonsform": {"kode": "FLI"}}] },
              "page": { "totalElements": 1 }
            }
            """;
        _wireMock.Given(Request.Create().WithPath("/enheter").WithParam("navn", "røa").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

        var result = await _sut.SearchByNameAsync("røa", 20, 0, CancellationToken.None);

        result.Hits.Should().HaveCount(1);
        result.Hits[0].Name.Should().Be("RØA ALLIANSEIDRETTSLAG");
    }

    [TestMethod]
    public async Task SearchByNameAsync_PageMetadataIsPassedThroughFromBrreg()
    {
        const string body = """
            {
              "_embedded": { "enheter": [{"organisasjonsnummer": "971491787", "navn": "FOO", "organisasjonsform": {"kode": "AS"}}] },
              "page": { "totalElements": 142, "totalPages": 6, "number": 2, "size": 25 }
            }
            """;
        _wireMock.Given(Request.Create().WithPath("/enheter").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

        var result = await _sut.SearchByNameAsync("foo", 25, 2, CancellationToken.None);

        result.Page.Should().Be(2);
        result.TotalPages.Should().Be(6);
        result.PageSize.Should().Be(25);
        result.TotalElements.Should().Be(142);
    }

    [TestMethod]
    public async Task SearchByNameAsync_PageZero_OmitsPageParameter()
    {
        // First-page requests should hit Brreg without a &page= query string. Brreg defaults to
        // page 0 anyway and skipping it keeps cache keys and proxy logs tidier.
        _wireMock.Given(Request.Create().WithPath("/enheter").WithParam("page").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(400));   // would 400 if page= is present
        _wireMock.Given(Request.Create().WithPath("/enheter").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"_embedded":{"enheter":[]},"page":{"totalElements":0,"totalPages":0,"number":0,"size":25}}"""));

        var result = await _sut.SearchByNameAsync("foo", 25, 0, CancellationToken.None);

        result.Hits.Should().BeEmpty();
    }

    [TestMethod]
    public async Task SearchByNameAsync_PageGreaterThanZero_IncludesPageParameter()
    {
        _wireMock.Given(Request.Create().WithPath("/enheter").WithParam("page", "3").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"_embedded":{"enheter":[{"organisasjonsnummer":"971491787","navn":"PAGE3","organisasjonsform":{"kode":"AS"}}]},"page":{"totalElements":100,"totalPages":4,"number":3,"size":25}}"""));

        var result = await _sut.SearchByNameAsync("foo", 25, 3, CancellationToken.None);

        result.Hits.Should().HaveCount(1);
        result.Hits[0].Name.Should().Be("PAGE3");
        result.Page.Should().Be(3);
    }

    [TestMethod]
    public async Task SearchByNameAsync_SkipsHitsWithoutOrgNumber()
    {
        // Brreg has never been seen to return an entity without orgnr, but the DTO field
        // is nullable so we defensively skip rather than emit a CompanySearchHit with empty
        // OrganizationNumber that would break the click-to-lookup flow downstream.
        const string body = """
            {
              "_embedded": { "enheter": [
                {"organisasjonsnummer": null, "navn": "WEIRD"},
                {"organisasjonsnummer": "971491787", "navn": "REAL"}
              ]},
              "page": { "totalElements": 2 }
            }
            """;
        _wireMock.Given(Request.Create().WithPath("/enheter").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

        var result = await _sut.SearchByNameAsync("query", 20, 0, CancellationToken.None);

        result.Hits.Should().HaveCount(1);
        result.Hits[0].Name.Should().Be("REAL");
    }
}
