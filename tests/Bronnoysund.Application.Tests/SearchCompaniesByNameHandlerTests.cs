// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Ports;
using Bronnoysund.Application.UseCases.SearchCompaniesByName;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Bronnoysund.Application.Tests;

[TestClass]
public class SearchCompaniesByNameHandlerTests
{
    private ICompanySearchProvider _provider = null!;
    private SearchCompaniesByNameHandler _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _provider = Substitute.For<ICompanySearchProvider>();
        _sut = new SearchCompaniesByNameHandler(_provider, NullLogger<SearchCompaniesByNameHandler>.Instance);
    }

    [TestMethod]
    public async Task ValidQuery_CallsProvider_AndReturnsFound()
    {
        var expected = new CompanySearchResult(
            [new CompanySearchHit("971032081", "STATENS VEGVESEN", "ORGL", "LILLEHAMMER")],
            TotalElements: 1);
        _provider.SearchByNameAsync("statens vegvesen", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.HandleAsync(new SearchCompaniesByNameQuery("statens vegvesen"), CancellationToken.None);

        result.Should().BeOfType<SearchCompaniesByNameResult.Found>()
            .Which.Result.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    [DataRow("a")]
    public async Task ShortOrEmptyQuery_ReturnsInvalidInput_WithoutCallingProvider(string? raw)
    {
        var result = await _sut.HandleAsync(new SearchCompaniesByNameQuery(raw), CancellationToken.None);

        result.Should().BeOfType<SearchCompaniesByNameResult.InvalidInput>()
            .Which.Message.Should().Contain("2 characters");
        await _provider.DidNotReceive().SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task QueryGetsTrimmedBeforePassingToProvider()
    {
        _provider.SearchByNameAsync("Statens vegvesen", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0));

        await _sut.HandleAsync(new SearchCompaniesByNameQuery("  Statens vegvesen  "), CancellationToken.None);

        await _provider.Received(1).SearchByNameAsync("Statens vegvesen", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    [DataRow(0, 1)]      // zero requested → clamped up to 1
    [DataRow(-5, 1)]     // negative requested → clamped up to 1
    [DataRow(50, 50)]    // in range → passed through
    [DataRow(1000, 100)] // huge requested → clamped down to 100
    public async Task PageSize_IsClampedToValidRange(int requested, int expected)
    {
        _provider.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0));

        await _sut.HandleAsync(new SearchCompaniesByNameQuery("Equinor", requested), CancellationToken.None);

        await _provider.Received(1).SearchByNameAsync("Equinor", expected, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(7)]
    public async Task RequestedPage_IsPassedToProvider(int page)
    {
        _provider.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0));

        await _sut.HandleAsync(new SearchCompaniesByNameQuery("Equinor", 25, page), CancellationToken.None);

        await _provider.Received(1).SearchByNameAsync("Equinor", 25, page, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task NegativePage_IsNormalizedToZero()
    {
        // Defensive: callers should always send page >= 0, but a stale UI link or hand-crafted
        // request could send something silly. Clamp instead of returning InvalidInput — there's
        // no legitimate "negative page" semantic and the user shouldn't see an error for it.
        _provider.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0));

        await _sut.HandleAsync(new SearchCompaniesByNameQuery("Equinor", 25, -3), CancellationToken.None);

        await _provider.Received(1).SearchByNameAsync("Equinor", 25, 0, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProviderThrows_ReturnsUnavailable()
    {
        _provider.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<CompanySearchResult>>(_ => throw new HttpRequestException("Brreg is down."));

        var result = await _sut.HandleAsync(new SearchCompaniesByNameQuery("Equinor"), CancellationToken.None);

        result.Should().BeOfType<SearchCompaniesByNameResult.Unavailable>()
            .Which.Message.Should().Contain("Brreg is down");
    }

    [TestMethod]
    public async Task CancellationException_IsNotCaughtAsUnavailable()
    {
        // OperationCanceledException should propagate so the caller can distinguish "user cancelled"
        // from "registry broke". Swallowing it would mask a legitimate cancellation.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _provider.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<CompanySearchResult>>(_ => throw new OperationCanceledException(cts.Token));

        var act = async () => await _sut.HandleAsync(new SearchCompaniesByNameQuery("Equinor"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
