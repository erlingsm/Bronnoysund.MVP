// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Dtos;

namespace Bronnoysund.Application.Results;

/// <summary>
/// Type-safe discriminated union for the result of a roles lookup. Mirrors
/// <see cref="CompanyLookupResult"/>: business outcomes (no roles, invalid input) are values,
/// only technical failures surface as <see cref="Unavailable"/>.
/// </summary>
public abstract record CompanyRolesResult
{
    private CompanyRolesResult() { }

    public sealed record Found(CompanyRolesResponse Roles) : CompanyRolesResult;

    public sealed record NotFound(string OrganizationNumber) : CompanyRolesResult;

    public sealed record InvalidInput(string Message) : CompanyRolesResult;

    public sealed record Unavailable(string Message) : CompanyRolesResult;
}
