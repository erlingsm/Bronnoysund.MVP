// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Dtos;
using Bronnoysund.Application.Ports;
using Bronnoysund.Application.Results;
using Bronnoysund.Application.UseCases.LookupCompany;
using Bronnoysund.Application.UseCases.SearchCompaniesByName;
using Bronnoysund.ViewModels.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace Bronnoysund.ViewModels;

public sealed partial class CompanyLookupViewModel(
    LookupCompanyHandler lookupHandler,
    SearchCompaniesByNameHandler searchHandler,
    IStringLocalizer<SharedResources> localizer) : ObservableObject
{
    [ObservableProperty]
    public partial string OrgNumberInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NameQueryInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsNameSearchMode { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial CompanyResponse? Found { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CompanySearchHit> SearchHits { get; set; } = [];

    [ObservableProperty]
    public partial int SearchTotalElements { get; set; }

    [RelayCommand]
    public async Task LookupAsync(CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(OrgNumberInput))
        {
            ErrorMessage = localizer["EnterOrgNumber"];
            return;
        }

        // Fresh user-initiated lookup — clear any prior search-hit breadcrumb.
        SearchHits = [];
        SearchTotalElements = 0;
        await LookupCoreAsync(ct);
    }

    /// <summary>
    /// Shared lookup pipeline used by both <see cref="LookupAsync"/> (fresh user input) and
    /// <see cref="SelectHitAsync"/> (drill-down from a search hit). Does not touch
    /// <c>SearchHits</c>: the public methods decide whether to preserve or clear it.
    /// </summary>
    private async Task LookupCoreAsync(CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        Found = null;

        try
        {
            var result = await lookupHandler.HandleAsync(new LookupCompanyQuery(OrgNumberInput), ct);
            switch (result)
            {
                case CompanyLookupResult.Found f:
                    Found = f.Company;
                    StatusMessage = localizer["FoundInRegistry"];
                    break;
                case CompanyLookupResult.NotFound nf:
                    ErrorMessage = localizer["NotFoundForOrgNumber", nf.OrganizationNumber];
                    break;
                case CompanyLookupResult.InvalidInput inv:
                    ErrorMessage = inv.Message;
                    break;
                case CompanyLookupResult.Unavailable u:
                    ErrorMessage = localizer["RegistryUnavailable", u.Message];
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SearchByNameAsync(CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        Found = null;
        SearchHits = [];
        SearchTotalElements = 0;

        try
        {
            var result = await searchHandler.HandleAsync(new SearchCompaniesByNameQuery(NameQueryInput), ct);
            switch (result)
            {
                case SearchCompaniesByNameResult.Found f:
                    SearchHits = f.Result.Hits;
                    SearchTotalElements = f.Result.TotalElements;
                    if (SearchHits.Count == 0)
                    {
                        ErrorMessage = localizer["NoHitsForName"];
                    }
                    break;
                case SearchCompaniesByNameResult.InvalidInput inv:
                    ErrorMessage = inv.Message;
                    break;
                case SearchCompaniesByNameResult.Unavailable u:
                    ErrorMessage = localizer["RegistryUnavailable", u.Message];
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectHitAsync(CompanySearchHit hit, CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }
        OrgNumberInput = hit.OrganizationNumber;
        await LookupCoreAsync(ct);
    }
}
