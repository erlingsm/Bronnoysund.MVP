// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Results;
using Bronnoysund.Domain;

namespace Bronnoysund.Application.Ports;

/// <summary>
/// Fetches the roles registered for an entity (Brreg Enhetsregisteret <c>/roller</c>).
/// Adapter implementation lives in the Infrastructure layer.
/// </summary>
public interface IRolesProvider
{
    Task<CompanyRolesResult> GetRolesAsync(OrganizationNumber org, CancellationToken ct);
}
