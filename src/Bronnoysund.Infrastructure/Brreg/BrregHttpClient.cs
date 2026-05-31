// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Http.Json;
using Bronnoysund.Domain;
using Bronnoysund.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bronnoysund.Infrastructure.Brreg;

/// <summary>
/// Typed HTTP client for Brreg. Wraps the open Enhetsregisteret endpoints (no auth needed).
/// All methods return null when the upstream returns 404/410 ("not found" as a business
/// outcome) and throw <see cref="BrregUnavailableException"/> on technical failures
/// (timeout, 5xx after Polly retry, circuit breaker open).
/// </summary>
internal sealed class BrregHttpClient(HttpClient http, ILogger<BrregHttpClient> logger)
{
    public Task<BrregEnhetDto?> GetEnhetAsync(OrganizationNumber org, CancellationToken ct)
        => GetJsonOrNullAsync<BrregEnhetDto>($"enheter/{org.Value}", org.Value, ct);

    public Task<BrregRollerDto?> GetRollerAsync(OrganizationNumber org, CancellationToken ct)
        => GetJsonOrNullAsync<BrregRollerDto>($"enheter/{org.Value}/roller", org.Value, ct);

    public Task<BrregEnheterPageDto?> SearchEnheterByNameAsync(string query, int size, int page, CancellationToken ct)
    {
        // Uri.EscapeDataString safely encodes Norwegian characters (æøå) and spaces.
        // Brreg only honors &page= when it is > 0; omitting it (rather than sending &page=0)
        // keeps cache keys upstream of any proxy clean and avoids an extra query-string knob
        // for the common first-page case.
        var encoded = Uri.EscapeDataString(query);
        var path = page > 0
            ? $"enheter?navn={encoded}&size={size}&page={page}"
            : $"enheter?navn={encoded}&size={size}";
        return GetJsonOrNullAsync<BrregEnheterPageDto>(path, query, ct);
    }

    private async Task<T?> GetJsonOrNullAsync<T>(string path, string contextValue, CancellationToken ct)
        where T : class
    {
        logger.LogDebug("Brreg GET {Path}", path);

        try
        {
            using var response = await http.GetAsync(path, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogInformation("Brreg returned 404 for {Path} ({Context})", path, contextValue);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Gone)
            {
                logger.LogInformation("Brreg returned 410 (deleted) for {Path} ({Context})", path, contextValue);
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new BrregUnavailableException(
                $"Brreg did not respond within the timeout for {path} ({contextValue}).", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new BrregUnavailableException(
                $"Could not contact Brreg for {path} ({contextValue}): {ex.Message}", ex);
        }
    }
}
