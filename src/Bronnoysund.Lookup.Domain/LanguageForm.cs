// SPDX-License-Identifier: MIT

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
