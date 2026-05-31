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
/// Verifies that BrregRolesProvider projects the grouped Brreg /roller payload into
/// CompanyRolesResponse: role groups kept intact, persons vs entities distinguished,
/// resigned/deregistered roles filtered out, valgtAv and birth dates carried through.
/// </summary>
[TestClass]
public class BrregRolesProviderMappingTests : IDisposable
{
    private WireMockServer _wireMock = null!;
    private HttpClient _httpClient = null!;
    private BrregRolesProvider _sut = null!;
    private static readonly OrganizationNumber Equinor = OrganizationNumber.Create("923609016");

    [TestInitialize]
    public void Setup()
    {
        _wireMock = WireMockServer.Start();
        _httpClient = new HttpClient { BaseAddress = new Uri(_wireMock.Url!) };
        var brreg = new BrregHttpClient(_httpClient, NullLogger<BrregHttpClient>.Instance);
        _sut = new BrregRolesProvider(brreg, NullLogger<BrregRolesProvider>.Instance);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _wireMock?.Stop();
        _wireMock?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void StubRoles(string body) =>
        _wireMock.Given(Request.Create().WithPath("/enheter/923609016/roller").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody(body));

    [TestMethod]
    public async Task GroupedPayload_KeepsGroupsAndMapsPersonsAndEntities()
    {
        const string body = """
            {
              "rollegrupper": [
                {
                  "type": { "kode": "DAGL", "beskrivelse": "Daglig leder" },
                  "roller": [
                    {
                      "type": { "kode": "DAGL", "beskrivelse": "Daglig leder" },
                      "person": { "fodselsdato": "1968-05-04", "navn": { "fornavn": "Anders", "etternavn": "Opedal" }, "erDoed": false },
                      "fratraadt": false, "avregistrert": false, "rekkefolge": 0
                    }
                  ]
                },
                {
                  "type": { "kode": "STYR", "beskrivelse": "Styre" },
                  "roller": [
                    {
                      "type": { "kode": "MEDL", "beskrivelse": "Styremedlem" },
                      "person": { "fodselsdato": "1966-11-14", "navn": { "fornavn": "Hilde", "etternavn": "Møllerstad" } },
                      "valgtAv": { "kode": "AREP", "beskrivelse": "Representant for de ansatte" },
                      "fratraadt": false, "avregistrert": false, "rekkefolge": 1
                    },
                    {
                      "type": { "kode": "LEDE", "beskrivelse": "Styrets leder" },
                      "person": { "fodselsdato": "1956-11-30", "navn": { "fornavn": "Jon Erik", "etternavn": "Reinhardsen" } },
                      "fratraadt": false, "avregistrert": false, "rekkefolge": 0
                    }
                  ]
                },
                {
                  "type": { "kode": "REVI", "beskrivelse": "Revisor" },
                  "roller": [
                    {
                      "type": { "kode": "REVI", "beskrivelse": "Ansvarlig revisor" },
                      "enhet": { "organisasjonsnummer": "967611600", "navn": ["ERNST & YOUNG AS"] },
                      "fratraadt": false, "avregistrert": false, "rekkefolge": 0
                    }
                  ]
                }
              ]
            }
            """;
        StubRoles(body);

        var result = await _sut.GetRolesAsync(Equinor, CancellationToken.None);

        var roles = result.Should().BeOfType<CompanyRolesResult.Found>().Subject.Roles;
        roles.OrganizationNumber.Should().Be("923609016");
        roles.Groups.Should().HaveCount(3);

        var dagl = roles.Groups[0];
        dagl.TypeCode.Should().Be("DAGL");
        dagl.TypeDescription.Should().Be("Daglig leder");
        dagl.Roles.Should().ContainSingle();
        dagl.Roles[0].Name.Should().Be("Anders Opedal");
        dagl.Roles[0].DateOfBirth.Should().Be(new DateOnly(1968, 5, 4));
        dagl.Roles[0].OrganizationNumber.Should().BeNull();

        // Styre roles are ordered by rekkefolge: leder (0) before the employee rep (1).
        var styre = roles.Groups[1];
        styre.TypeDescription.Should().Be("Styre");
        styre.Roles.Should().HaveCount(2);
        styre.Roles[0].Name.Should().Be("Jon Erik Reinhardsen");
        styre.Roles[0].RoleTypeDescription.Should().Be("Styrets leder");
        styre.Roles[1].Name.Should().Be("Hilde Møllerstad");
        styre.Roles[1].ElectedBy.Should().Be("Representant for de ansatte");

        // Entity-held role (auditor) carries the orgnr and no birth date.
        var revi = roles.Groups[2];
        revi.Roles[0].Name.Should().Be("ERNST & YOUNG AS");
        revi.Roles[0].OrganizationNumber.Should().Be("967611600");
        revi.Roles[0].DateOfBirth.Should().BeNull();
    }

    [TestMethod]
    public async Task ResignedAndDeregisteredRoles_AreFilteredOut_AndEmptyGroupsDropped()
    {
        const string body = """
            {
              "rollegrupper": [
                {
                  "type": { "kode": "STYR", "beskrivelse": "Styre" },
                  "roller": [
                    {
                      "type": { "kode": "MEDL", "beskrivelse": "Styremedlem" },
                      "person": { "navn": { "fornavn": "Aktiv", "etternavn": "Person" } },
                      "fratraadt": false, "avregistrert": false, "rekkefolge": 0
                    },
                    {
                      "type": { "kode": "MEDL", "beskrivelse": "Styremedlem" },
                      "person": { "navn": { "fornavn": "Fratrådt", "etternavn": "Person" } },
                      "fratraadt": true, "avregistrert": false, "rekkefolge": 1
                    }
                  ]
                },
                {
                  "type": { "kode": "DAGL", "beskrivelse": "Daglig leder" },
                  "roller": [
                    {
                      "type": { "kode": "DAGL", "beskrivelse": "Daglig leder" },
                      "person": { "navn": { "fornavn": "Avregistrert", "etternavn": "Leder" } },
                      "fratraadt": false, "avregistrert": true, "rekkefolge": 0
                    }
                  ]
                }
              ]
            }
            """;
        StubRoles(body);

        var result = await _sut.GetRolesAsync(Equinor, CancellationToken.None);

        var roles = result.Should().BeOfType<CompanyRolesResult.Found>().Subject.Roles;
        // The DAGL group is dropped entirely (its only role was deregistered).
        roles.Groups.Should().ContainSingle();
        roles.Groups[0].TypeCode.Should().Be("STYR");
        roles.Groups[0].Roles.Should().ContainSingle();
        roles.Groups[0].Roles[0].Name.Should().Be("Aktiv Person");
    }

    [TestMethod]
    public async Task NotFound_WhenBrregReturns404()
    {
        _wireMock.Given(Request.Create().WithPath("/enheter/923609016/roller").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var result = await _sut.GetRolesAsync(Equinor, CancellationToken.None);

        result.Should().BeOfType<CompanyRolesResult.NotFound>()
            .Which.OrganizationNumber.Should().Be("923609016");
    }

    [TestMethod]
    public async Task Found_WithEmptyGroups_WhenNoRollegrupper()
    {
        StubRoles("""{ "rollegrupper": [] }""");

        var result = await _sut.GetRolesAsync(Equinor, CancellationToken.None);

        result.Should().BeOfType<CompanyRolesResult.Found>()
            .Which.Roles.Groups.Should().BeEmpty();
    }
}
