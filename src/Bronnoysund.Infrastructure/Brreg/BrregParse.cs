// SPDX-License-Identifier: MIT

using System.Globalization;

namespace Bronnoysund.Infrastructure.Brreg;

/// <summary>
/// Shared parsing helpers for Brreg JSON values. Brreg serialises dates as ISO <c>yyyy-MM-dd</c>
/// strings and uses empty strings interchangeably with absent fields; both provider adapters
/// (entity and roles) need the same coercions, so they live here rather than being duplicated.
/// </summary>
internal static class BrregParse
{
    /// <summary>Collapse null/whitespace strings to null so empty Brreg fields read as absent.</summary>
    public static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>Parse a Brreg ISO date (<c>yyyy-MM-dd</c>); null/blank/malformed yields null.</summary>
    public static DateOnly? Date(string? iso) =>
        string.IsNullOrWhiteSpace(iso)
            ? null
            : DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : null;
}
