// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

namespace Bronnoysund.Lookup.Application.UseCases.LookupCompany;

/// <summary>Request to look up a company based on an organization-number input (raw string).</summary>
public sealed record LookupCompanyQuery(string OrganizationNumberInput);
