// SPDX-License-Identifier: MIT

using Bronnoysund.Lookup.Application.Ports;
using Bronnoysund.Lookup.Infrastructure.Brreg;
using Bronnoysund.Lookup.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bronnoysund.Lookup.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBronnoysundInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<BrregOptions>()
            .Bind(configuration.GetSection(BrregOptions.SectionName))
            .ValidateOnStart();

        services.AddHybridCache();

        services.AddHttpClient<BrregHttpClient>((sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<BrregOptions>>().Value;
            http.BaseAddress = new Uri(opts.BaseUrl);
            http.Timeout = opts.RequestTimeout;
            http.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).AddStandardResilienceHandler();

        services.AddSingleton<BrregCompanyProvider>();
        services.AddSingleton<ICompanyProvider>(sp =>
            new CachingCompanyProvider(
                inner: sp.GetRequiredService<BrregCompanyProvider>(),
                cache: sp.GetRequiredService<HybridCache>(),
                options: sp.GetRequiredService<IOptions<BrregOptions>>(),
                logger: sp.GetRequiredService<ILogger<CachingCompanyProvider>>()));

        services.AddSingleton<ICompanySearchProvider, BrregCompanySearchProvider>();

        return services;
    }
}
