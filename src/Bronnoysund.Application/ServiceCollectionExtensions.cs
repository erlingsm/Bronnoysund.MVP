// SPDX-License-Identifier: MIT

using Bronnoysund.Application.UseCases.LookupCompany;
using Bronnoysund.Application.UseCases.SearchCompaniesByName;
using Microsoft.Extensions.DependencyInjection;

namespace Bronnoysund.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBronnoysundApplication(this IServiceCollection services)
    {
        services.AddTransient<LookupCompanyHandler>();
        services.AddTransient<SearchCompaniesByNameHandler>();
        return services;
    }
}
