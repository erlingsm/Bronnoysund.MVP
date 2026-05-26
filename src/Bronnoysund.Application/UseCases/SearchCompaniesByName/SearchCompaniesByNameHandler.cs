// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Ports;
using Microsoft.Extensions.Logging;

namespace Bronnoysund.Application.UseCases.SearchCompaniesByName;

/// <summary>
/// Use case for free-text name search. Validates the query (minimum 2 characters; otherwise Brreg
/// returns 400 anyway and we want to fail fast with a friendly message), clamps the page size
/// to a safe range, normalizes the page index, and delegates to <see cref="ICompanySearchProvider"/>.
/// </summary>
public sealed class SearchCompaniesByNameHandler(
    ICompanySearchProvider provider,
    ILogger<SearchCompaniesByNameHandler> logger)
{
    private const int MinQueryLength = 2;
    public const int DefaultPageSize = 25;
    public const int AbsoluteMaxPageSize = 100;

    public async Task<SearchCompaniesByNameResult> HandleAsync(SearchCompaniesByNameQuery query, CancellationToken ct)
    {
        var trimmed = query.Name?.Trim() ?? string.Empty;
        if (trimmed.Length < MinQueryLength)
        {
            return new SearchCompaniesByNameResult.InvalidInput(
                $"Search must be at least {MinQueryLength} characters.");
        }

        var pageSize = Math.Clamp(query.MaxResults ?? DefaultPageSize, 1, AbsoluteMaxPageSize);
        var page = Math.Max(0, query.Page);

        logger.LogInformation(
            "Searching companies by name '{Query}' (page {Page}, size {Size})",
            trimmed, page, pageSize);

        try
        {
            var result = await provider.SearchByNameAsync(trimmed, pageSize, page, ct);
            return new SearchCompaniesByNameResult.Found(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Company search failed for '{Query}'", trimmed);
            return new SearchCompaniesByNameResult.Unavailable(ex.Message);
        }
    }
}

public sealed record SearchCompaniesByNameQuery(string? Name, int? MaxResults = null, int Page = 0);

public abstract record SearchCompaniesByNameResult
{
    public sealed record Found(CompanySearchResult Result) : SearchCompaniesByNameResult;
    public sealed record InvalidInput(string Message) : SearchCompaniesByNameResult;
    public sealed record Unavailable(string Message) : SearchCompaniesByNameResult;
}
