// SPDX-License-Identifier: MIT

using Bronnoysund.Application.Results;
using Bronnoysund.Domain;

namespace Bronnoysund.Application.Ports;

/// <summary>
/// Fetches the core data for an entity (Brreg Enhetsregisteret).
/// Adapter implementation lives in the Infrastructure layer.
/// </summary>
public interface ICompanyProvider
{
    Task<CompanyLookupResult> LookupAsync(OrganizationNumber org, CancellationToken ct);
}
