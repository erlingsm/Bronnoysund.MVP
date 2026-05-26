// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Ports;
using Bronnoysund.Infrastructure;
using Bronnoysund.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Bronnoysund.Infrastructure.Tests;

/// <summary>
/// Verifies the caching decorator delegates to the inner provider exactly once per unique
/// (query, pageSize, page) combination — so a user paging through results doesn't re-hit Brreg
/// for pages they've already visited within the TTL window.
/// </summary>
[TestClass]
public sealed class CachingCompanySearchProviderTests
{
    private static CachingCompanySearchProvider BuildSut(ICompanySearchProvider inner, TimeSpan? ttl = null)
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<HybridCache>();
        var options = Options.Create(new BrregOptions { SearchCacheTtl = ttl ?? TimeSpan.FromMinutes(5) });
        return new CachingCompanySearchProvider(inner, cache, options, NullLogger<CachingCompanySearchProvider>.Instance);
    }

    [TestMethod]
    public async Task SameQueryAndPage_CallsInnerOnce()
    {
        var inner = Substitute.For<ICompanySearchProvider>();
        inner.SearchByNameAsync("equinor", 25, 0, Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0, Page: 0, TotalPages: 1, PageSize: 25));
        var sut = BuildSut(inner);

        await sut.SearchByNameAsync("equinor", 25, 0, CancellationToken.None);
        await sut.SearchByNameAsync("equinor", 25, 0, CancellationToken.None);
        await sut.SearchByNameAsync("equinor", 25, 0, CancellationToken.None);

        await inner.Received(1).SearchByNameAsync("equinor", 25, 0, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task DifferentPage_CallsInnerForEachPage()
    {
        var inner = Substitute.For<ICompanySearchProvider>();
        inner.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0, 0, 1, 25));
        var sut = BuildSut(inner);

        await sut.SearchByNameAsync("equinor", 25, 0, CancellationToken.None);
        await sut.SearchByNameAsync("equinor", 25, 1, CancellationToken.None);
        await sut.SearchByNameAsync("equinor", 25, 0, CancellationToken.None);   // back to page 0 — cached
        await sut.SearchByNameAsync("equinor", 25, 2, CancellationToken.None);

        await inner.Received(1).SearchByNameAsync("equinor", 25, 0, Arg.Any<CancellationToken>());
        await inner.Received(1).SearchByNameAsync("equinor", 25, 1, Arg.Any<CancellationToken>());
        await inner.Received(1).SearchByNameAsync("equinor", 25, 2, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task DifferentPageSize_CachesSeparately()
    {
        // Going from 25-per-page to 50-per-page is a different cache entry — the page shape
        // changes, so re-fetching is the right call.
        var inner = Substitute.For<ICompanySearchProvider>();
        inner.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0, 0, 1, 25));
        var sut = BuildSut(inner);

        await sut.SearchByNameAsync("equinor", 25, 0, CancellationToken.None);
        await sut.SearchByNameAsync("equinor", 50, 0, CancellationToken.None);

        await inner.Received(1).SearchByNameAsync("equinor", 25, 0, Arg.Any<CancellationToken>());
        await inner.Received(1).SearchByNameAsync("equinor", 50, 0, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task QueryCasingIsNormalized_SoEquinorAndEQUINORShareCache()
    {
        var inner = Substitute.For<ICompanySearchProvider>();
        inner.SearchByNameAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new CompanySearchResult([], 0, 0, 1, 25));
        var sut = BuildSut(inner);

        await sut.SearchByNameAsync("Equinor", 25, 0, CancellationToken.None);
        await sut.SearchByNameAsync("EQUINOR", 25, 0, CancellationToken.None);
        await sut.SearchByNameAsync("equinor", 25, 0, CancellationToken.None);

        await inner.Received(1).SearchByNameAsync(Arg.Any<string>(), 25, 0, Arg.Any<CancellationToken>());
    }
}
