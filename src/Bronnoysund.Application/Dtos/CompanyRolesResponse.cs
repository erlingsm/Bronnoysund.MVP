// SPDX-License-Identifier: MIT

namespace Bronnoysund.Application.Dtos;

/// <summary>
/// Roles registered for an entity, sourced from the open Brreg
/// <c>/enheter/{orgnr}/roller</c> endpoint. Roles are kept in their Brreg
/// <see cref="RoleGroup">role groups</see> (Styre, Daglig leder, Revisor, …) rather than
/// flattened, so the UI can present them grouped the way the registry does. Only open data
/// is exposed — names and birth dates, never fødselsnummer (that requires Maskinporten).
/// </summary>
public sealed record CompanyRolesResponse(
    string OrganizationNumber,
    IReadOnlyList<RoleGroup> Groups
);

/// <summary>
/// A Brreg role group — e.g. "Styre" (STYR) or "Daglig leder" (DAGL) — holding every active
/// role of that group. Empty groups (all roles resigned/deregistered) are dropped upstream.
/// </summary>
public sealed record RoleGroup(
    string TypeCode,
    string TypeDescription,
    IReadOnlyList<RoleHolder> Roles
);

/// <summary>
/// A single active role held by a person or another entity. <see cref="OrganizationNumber"/>
/// is set when the holder is an entity (e.g. an audit firm); <see cref="DateOfBirth"/> is set
/// when the holder is a person. <see cref="ElectedBy"/> mirrors Brreg's <c>valgtAv</c>
/// (e.g. "Representant for de ansatte") when present.
/// </summary>
public sealed record RoleHolder(
    string RoleTypeCode,
    string RoleTypeDescription,
    string Name,
    string? OrganizationNumber = null,
    DateOnly? DateOfBirth = null,
    bool IsDeceased = false,
    string? ElectedBy = null
);
