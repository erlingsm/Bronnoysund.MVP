// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Dtos;
using Bronnoysund.Application.Ports;
using Bronnoysund.Application.Results;
using Bronnoysund.Application.UseCases.LookupCompany;
using Bronnoysund.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Bronnoysund.Application.Tests;

[TestClass]
public class LookupCompanyHandlerTests
{
    private ICompanyProvider _provider = null!;
    private LookupCompanyHandler _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _provider = Substitute.For<ICompanyProvider>();
        _sut = new LookupCompanyHandler(_provider, NullLogger<LookupCompanyHandler>.Instance);
    }

    [TestMethod]
    public async Task ValidOrgNumber_CallsProvider_AndReturnsFound()
    {
        var expected = new CompanyResponse("919300388", "Equinor ASA", "AS", "Bokmål");
        _provider.LookupAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupResult.Found(expected));

        var result = await _sut.HandleAsync(new LookupCompanyQuery("919300388"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.Found>()
            .Which.Company.Should().Be(expected);
    }

    [TestMethod]
    public async Task InvalidOrgNumber_ReturnsInvalidInput_WithoutCallingProvider()
    {
        var result = await _sut.HandleAsync(new LookupCompanyQuery("12345"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.InvalidInput>();
        await _provider.DidNotReceive().LookupAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task EmptyInput_ReturnsInvalidInput()
    {
        var result = await _sut.HandleAsync(new LookupCompanyQuery(""), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.InvalidInput>();
    }

    [TestMethod]
    public async Task ValidOrgNumberButProviderReturnsNotFound_PropagatesNotFound()
    {
        _provider.LookupAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupResult.NotFound("919300388"));

        var result = await _sut.HandleAsync(new LookupCompanyQuery("919300388"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.NotFound>()
            .Which.OrganizationNumber.Should().Be("919300388");
    }

    [TestMethod]
    public async Task ValidOrgNumberButProviderReturnsUnavailable_PropagatesUnavailable()
    {
        _provider.LookupAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupResult.Unavailable("Brreg is down."));

        var result = await _sut.HandleAsync(new LookupCompanyQuery("919300388"), CancellationToken.None);

        result.Should().BeOfType<CompanyLookupResult.Unavailable>()
            .Which.Message.Should().Be("Brreg is down.");
    }
}
