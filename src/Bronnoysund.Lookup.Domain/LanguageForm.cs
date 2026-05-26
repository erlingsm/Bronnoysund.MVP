// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

namespace Bronnoysund.Lookup.Domain;

/// <summary>
/// Norwegian written-language form (målform) an entity is registered with, per the Brreg field "maalform".
/// </summary>
public enum LanguageForm
{
    Unknown = 0,
    Bokmål = 1,
    Nynorsk = 2,
}
