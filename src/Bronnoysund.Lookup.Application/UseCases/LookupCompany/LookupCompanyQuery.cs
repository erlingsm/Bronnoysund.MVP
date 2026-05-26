// SPDX-License-Identifier: MIT

namespace Bronnoysund.Lookup.Application.UseCases.LookupCompany;

/// <summary>Request to look up a company based on an organization-number input (raw string).</summary>
public sealed record LookupCompanyQuery(string OrganizationNumberInput);
