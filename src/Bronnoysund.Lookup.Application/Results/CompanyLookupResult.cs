// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Application.Dtos;

namespace Bronnoysund.Lookup.Application.Results;

/// <summary>
/// Type-safe discriminated union for the result of a company lookup. Avoids exceptions for
/// business outcomes (404, validation errors) — only technical failures are thrown as exceptions.
/// </summary>
public abstract record CompanyLookupResult
{
    private CompanyLookupResult() { }

    public sealed record Found(CompanyResponse Company) : CompanyLookupResult;

    public sealed record NotFound(string OrganizationNumber) : CompanyLookupResult;

    public sealed record InvalidInput(string Message) : CompanyLookupResult;

    public sealed record Unavailable(string Message) : CompanyLookupResult;
}
