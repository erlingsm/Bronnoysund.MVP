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

    /// <summary>Zero-indexed current page (matches Brreg's wire format).</summary>
    [ObservableProperty]
    public partial int CurrentPage { get; set; }

    /// <summary>Page size in the most recent search response (echoed from Brreg).</summary>
    [ObservableProperty]
    public partial int PageSize { get; set; } = SearchCompaniesByNameHandler.DefaultPageSize;

    /// <summary>Total pages available for the current query/pageSize, per Brreg.</summary>
    [ObservableProperty]
    public partial int TotalPages { get; set; }

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
        TotalPages = 0;
        CurrentPage = 0;
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

    /// <summary>New name search — always starts at page 0 and clears any previous result.</summary>
    [RelayCommand]
    public async Task SearchByNameAsync(CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }
        CurrentPage = 0;
        await SearchCoreAsync(ct);
    }

    /// <summary>
    /// Navigate to <paramref name="page"/> (zero-indexed) within the current query. Out-of-range
    /// requests are ignored. The HybridCache-backed decorator on the search provider serves
    /// pages already visited within the TTL window without re-hitting Brreg.
    /// </summary>
    public async Task GoToPageAsync(int page, CancellationToken ct)
    {
        if (IsBusy || page < 0 || page >= TotalPages || page == CurrentPage)
        {
            return;
        }
        CurrentPage = page;
        await SearchCoreAsync(ct);
    }

    /// <summary>
    /// Change page size and re-search at page 0. Existing TotalPages is recalculated by the
    /// backend, so a previous "page 7 of 10" can legitimately become "page 0 of 3" after
    /// bumping the size up.
    /// </summary>
    public async Task ChangePageSizeAsync(int newPageSize, CancellationToken ct)
    {
        if (IsBusy || newPageSize == PageSize)
        {
            return;
        }
        PageSize = newPageSize;
        CurrentPage = 0;
        await SearchCoreAsync(ct);
    }

    private async Task SearchCoreAsync(CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        Found = null;
        SearchHits = [];

        try
        {
            var result = await searchHandler.HandleAsync(
                new SearchCompaniesByNameQuery(NameQueryInput, PageSize, CurrentPage), ct);
            switch (result)
            {
                case SearchCompaniesByNameResult.Found f:
                    SearchHits = f.Result.Hits;
                    SearchTotalElements = f.Result.TotalElements;
                    TotalPages = f.Result.TotalPages;
                    CurrentPage = f.Result.Page;     // sync with what backend echoed
                    PageSize = f.Result.PageSize;    // ditto — Brreg may have clamped
                    if (SearchHits.Count == 0)
                    {
                        ErrorMessage = localizer["NoHitsForName"];
                    }
                    break;
                case SearchCompaniesByNameResult.InvalidInput inv:
                    ErrorMessage = inv.Message;
                    SearchTotalElements = 0;
                    TotalPages = 0;
                    break;
                case SearchCompaniesByNameResult.Unavailable u:
                    ErrorMessage = localizer["RegistryUnavailable", u.Message];
                    SearchTotalElements = 0;
                    TotalPages = 0;
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

    /// <summary>
    /// Wipe all transient form state — input fields, error/status messages, search results,
    /// pagination, drilled-down detail. Triggered by clicking the app title in the header.
    /// The HybridCache layer in Infrastructure is untouched, so the next search/lookup for
    /// the same orgnr or query is served from cache.
    /// </summary>
    public void Reset()
    {
        if (IsBusy)
        {
            return;
        }
        OrgNumberInput = string.Empty;
        NameQueryInput = string.Empty;
        IsNameSearchMode = false;
        Found = null;
        ErrorMessage = null;
        StatusMessage = null;
        SearchHits = [];
        SearchTotalElements = 0;
        CurrentPage = 0;
        PageSize = SearchCompaniesByNameHandler.DefaultPageSize;
        TotalPages = 0;
    }
}
