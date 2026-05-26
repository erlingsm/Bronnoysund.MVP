// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Application.Results;
using Bronnoysund.Lookup.Domain;

namespace Bronnoysund.Lookup.Application.Ports;

/// <summary>
/// Fetches the core data for an entity (Brreg Enhetsregisteret).
/// Adapter implementation lives in the Infrastructure layer.
/// </summary>
public interface ICompanyProvider
{
    Task<CompanyLookupResult> LookupAsync(OrganizationNumber org, CancellationToken ct);
}
