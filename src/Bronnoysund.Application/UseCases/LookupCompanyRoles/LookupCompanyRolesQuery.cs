// SPDX-License-Identifier: MIT

namespace Bronnoysund.Application.UseCases.LookupCompanyRoles;

/// <summary>Query for the roles of the entity with the given organization number.</summary>
public sealed record LookupCompanyRolesQuery(string OrganizationNumberInput);
