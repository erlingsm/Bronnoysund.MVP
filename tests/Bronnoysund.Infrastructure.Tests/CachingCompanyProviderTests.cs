// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Dtos;
using Bronnoysund.Application.Results;
using Bronnoysund.Infrastructure.Caching;
using FluentAssertions;
using Xunit;

namespace Bronnoysund.Infrastructure.Tests;

/// <summary>
/// Round-trip tests for <see cref="CachedLookup"/>: every reachable
/// <see cref="CompanyLookupResult"/> case must survive From()/ToResult() with full fidelity.
/// Catches regressions when adding new result subtypes to the union — if a new case is
/// added without updating the cache layer, From/ToResult will silently drop it.
/// </summary>
public class CachingCompanyProviderTests
{
    [Fact]
    public void RoundTrip_Found_PreservesCompanyResponse()
    {
        var company = new CompanyResponse(
            OrganizationNumber: "919300388",
            OrganizationName: "Equinor ASA",
            CompanyType: "AS",
            LanguageForm: "Bokmål");
        var original = new CompanyLookupResult.Found(company);

        var roundtripped = CachedLookup.From(original).ToResult();

        roundtripped.Should().BeOfType<CompanyLookupResult.Found>()
            .Which.Company.Should().BeEquivalentTo(company);
    }

    [Fact]
    public void RoundTrip_NotFound_PreservesOrgNumber()
    {
        var original = new CompanyLookupResult.NotFound("919300389");

        var roundtripped = CachedLookup.From(original).ToResult();

        roundtripped.Should().BeOfType<CompanyLookupResult.NotFound>()
            .Which.OrganizationNumber.Should().Be("919300389");
    }

    [Fact]
    public void RoundTrip_Unavailable_PreservesMessage()
    {
        var original = new CompanyLookupResult.Unavailable("Brreg timed out after 10 seconds");

        var roundtripped = CachedLookup.From(original).ToResult();

        roundtripped.Should().BeOfType<CompanyLookupResult.Unavailable>()
            .Which.Message.Should().Be("Brreg timed out after 10 seconds");
    }

    [Fact]
    public void ToResult_EmptyCachedLookup_Throws()
    {
        var empty = new CachedLookup(null, null, null);

        var act = () => empty.ToResult();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("CachedLookup is empty*");
    }
}
