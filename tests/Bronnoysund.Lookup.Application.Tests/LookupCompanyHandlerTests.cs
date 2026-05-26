// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Application.Dtos;
using Bronnoysund.Lookup.Application.Ports;
using Bronnoysund.Lookup.Application.Results;
using Bronnoysund.Lookup.Application.UseCases.LookupCompany;
using Bronnoysund.Lookup.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bronnoysund.Lookup.Application.Tests;

public class LookupCompanyHandlerTests
{
    private readonly ICompanyProvider _provider = Substitute.For<ICompanyProvider>();
    private readonly LookupCompanyHandler _sut;

    public LookupCompanyHandlerTests()
    {
        _sut = new LookupCompanyHandler(_provider, NullLogger<LookupCompanyHandler>.Instance);
    }

    [Fact]
    public async Task ValidOrgNumber_CallsProvider_AndReturnsFound()
    {
        var expected = new CompanyResponse("919300388", "Equinor ASA", "AS", "Bokmål");
        _provider.LookupAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupResult.Found(expected));

        var result = await _sut.HandleAsync(new LookupCompanyQuery("919300388"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.Found>()
            .Which.Company.Should().Be(expected);
    }

    [Fact]
    public async Task InvalidOrgNumber_ReturnsInvalidInput_WithoutCallingProvider()
    {
        var result = await _sut.HandleAsync(new LookupCompanyQuery("12345"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.InvalidInput>();
        await _provider.DidNotReceive().LookupAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyInput_ReturnsInvalidInput()
    {
        var result = await _sut.HandleAsync(new LookupCompanyQuery(""), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.InvalidInput>();
    }

    [Fact]
    public async Task ValidOrgNumberButProviderReturnsNotFound_PropagatesNotFound()
    {
        _provider.LookupAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupResult.NotFound("919300388"));

        var result = await _sut.HandleAsync(new LookupCompanyQuery("919300388"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.NotFound>()
            .Which.OrganizationNumber.Should().Be("919300388");
    }

    [Fact]
    public async Task ValidOrgNumberButProviderReturnsUnavailable_PropagatesUnavailable()
    {
        _provider.LookupAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupResult.Unavailable("Brreg is down."));

        var result = await _sut.HandleAsync(new LookupCompanyQuery("919300388"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.Unavailable>()
            .Which.Message.Should().Be("Brreg is down.");
    }
}
