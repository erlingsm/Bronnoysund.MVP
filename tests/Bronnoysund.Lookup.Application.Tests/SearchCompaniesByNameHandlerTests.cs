// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Application.Ports;
using Bronnoysund.Lookup.Application.UseCases.SearchCompaniesByName;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bronnoysund.Lookup.Application.Tests;

public class SearchCompaniesByNameHandlerTests
{
    private readonly ICompanySearchProvider _provider = Substitute.For<ICompanySearchProvider>();
    private readonly SearchCompaniesByNameHandler _sut;

    public SearchCompaniesByNameHandlerTests()
    {
        _sut = new SearchCompaniesByNameHandler(_provider, NullLogger<SearchCompaniesByNameHandler>.Instance);
    }

    [Fact]
    public async Task ValidQuery_CallsProvider_AndReturnsFound()
    {
        var expected = new CompanySearchResult(
            [new CompanySearchHit("971032081", "STATENS VEGVESEN", "ORGL", "LILLEHAMMER")],
            TotalElements: 1);
        _provider.SearchByNameAsync("statens vegvesen", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.HandleAsync(new SearchCompaniesByNameQuery("statens vegvesen"), CancellationToken.None);

        result.Should().BeOfType<SearchCompaniesByNameResult.Found>()
            .Which.Result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("a")]
    public async Task ShortOrEmptyQuery_ReturnsInvalidInput_WithoutCallingProvider(string? raw)
    {
        var result = await _sut.HandleAsync(new SearchCompaniesByNameQuery(raw), CancellationToken.None);

        result.Should().BeOfType<SearchCompaniesByNameResult.InvalidInput>()
            .Which.Message.Should().Contain("2 characters");
        await _provider.DidNotReceive().SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryGetsTrimmedBeforePassingToProvider()
    {
        _provider.SearchByNameAsync("Statens vegvesen", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0));

        await _sut.HandleAsync(new SearchCompaniesByNameQuery("  Statens vegvesen  "), CancellationToken.None);

        await _provider.Received(1).SearchByNameAsync("Statens vegvesen", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 1)]      // zero requested → clamped up to 1
    [InlineData(-5, 1)]     // negative requested → clamped up to 1
    [InlineData(50, 50)]    // in range → passed through
    [InlineData(1000, 100)] // huge requested → clamped down to 100
    public async Task MaxResults_IsClampedToValidRange(int requested, int expected)
    {
        _provider.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0));

        await _sut.HandleAsync(new SearchCompaniesByNameQuery("Equinor", requested), CancellationToken.None);

        await _provider.Received(1).SearchByNameAsync("Equinor", expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProviderThrows_ReturnsUnavailable()
    {
        _provider.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<CompanySearchResult>>(_ => throw new HttpRequestException("Brreg is down."));

        var result = await _sut.HandleAsync(new SearchCompaniesByNameQuery("Equinor"), CancellationToken.None);

        result.Should().BeOfType<SearchCompaniesByNameResult.Unavailable>()
            .Which.Message.Should().Contain("Brreg is down");
    }

    [Fact]
    public async Task CancellationException_IsNotCaughtAsUnavailable()
    {
        // OperationCanceledException should propagate so the caller can distinguish "user cancelled"
        // from "registry broke". Swallowing it would mask a legitimate cancellation.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _provider.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<CompanySearchResult>>(_ => throw new OperationCanceledException(cts.Token));

        var act = async () => await _sut.HandleAsync(new SearchCompaniesByNameQuery("Equinor"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
