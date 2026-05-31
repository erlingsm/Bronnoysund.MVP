// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Dtos;
using Bronnoysund.Application.Ports;
using Bronnoysund.Application.Results;
using Bronnoysund.Domain;
using Bronnoysund.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Bronnoysund.Infrastructure.Tests;

/// <summary>
/// Behavioural tests for the shared <see cref="BrregCache"/> eviction policy via
/// <see cref="CachingRolesProvider"/> over a real HybridCache: stable results (Found) are cached
/// for the TTL, while a transient Unavailable is evicted so the next request re-checks Brreg
/// instead of being stuck on a cached outage for the full 24 h.
/// </summary>
[TestClass]
public class CachingRolesProviderEvictionTests
{
    private static readonly OrganizationNumber Org = OrganizationNumber.Create("923609016");

    private static (CachingRolesProvider sut, IRolesProvider inner) Build()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();

        var inner = Substitute.For<IRolesProvider>();
        var sut = new CachingRolesProvider(
            inner, cache, Options.Create(new BrregOptions()),
            NullLogger<CachingRolesProvider>.Instance);
        return (sut, inner);
    }

    [TestMethod]
    public async Task Found_IsCached_InnerCalledOnce()
    {
        var (sut, inner) = Build();
        var found = new CompanyRolesResult.Found(new CompanyRolesResponse("923609016", []));
        inner.GetRolesAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>()).Returns(found);

        await sut.GetRolesAsync(Org, CancellationToken.None);
        await sut.GetRolesAsync(Org, CancellationToken.None);

        await inner.Received(1).GetRolesAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Unavailable_IsNotRetained_NextCallReachesBrregAgain()
    {
        var (sut, inner) = Build();
        // First call: Brreg is down; second call: it has recovered.
        inner.GetRolesAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>())
            .Returns(
                new CompanyRolesResult.Unavailable("Brreg timed out"),
                new CompanyRolesResult.Found(new CompanyRolesResponse("923609016", [])));

        var first = await sut.GetRolesAsync(Org, CancellationToken.None);
        var second = await sut.GetRolesAsync(Org, CancellationToken.None);

        first.Should().BeOfType<CompanyRolesResult.Unavailable>();
        second.Should().BeOfType<CompanyRolesResult.Found>();
        await inner.Received(2).GetRolesAsync(Arg.Any<OrganizationNumber>(), Arg.Any<CancellationToken>());
    }
}
