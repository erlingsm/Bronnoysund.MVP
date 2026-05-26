// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

namespace Bronnoysund.Lookup.Domain;

/// <summary>
/// Norwegian entity identified by its organization number.
/// Domain entity with an immutable structure (record). Identity = <see cref="OrganizationNumber"/>.
/// </summary>
public sealed record Company(
    OrganizationNumber OrganizationNumber,
    string Name,
    string OrganizationFormCode,
    LanguageForm LanguageForm
);
