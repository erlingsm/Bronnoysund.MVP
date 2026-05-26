// SPDX-License-Identifier: MIT

using Bronnoysund.Lookup.Application.UseCases.LookupCompany;
using Bronnoysund.Lookup.Application.UseCases.SearchCompaniesByName;
using Bronnoysund.Lookup.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Bronnoysund.Lookup.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBronnoysundApplication(this IServiceCollection services)
    {
        services.AddTransient<LookupCompanyHandler>();
        services.AddTransient<SearchCompaniesByNameHandler>();
        services.AddValidatorsFromAssemblyContaining<OrganizationNumberValidator>();
        return services;
    }
}
