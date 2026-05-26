// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

namespace Bronnoysund.Lookup.Application.Dtos;

/// <summary>
/// English-field response for organization-number lookups, per the original task requirements.
/// The first four fields cover the MVP contract; subsequent optional fields are populated from
/// the open Brreg entity payload when available, and stay null for entities that lack them.
/// All additions have null defaults so the original API contract is preserved.
/// </summary>
public sealed record CompanyResponse(
    string OrganizationNumber,
    string OrganizationName,
    string CompanyType,
    string LanguageForm,
    string? Website = null,
    string? Email = null,
    string? Phone = null,
    string? MobilePhone = null,
    PostalAddress? BusinessAddress = null,
    PostalAddress? PostalAddress = null,
    IndustryCode? PrimaryIndustry = null,
    int? EmployeeCount = null,
    string? SectorCode = null,
    string? SectorDescription = null,
    DateOnly? FoundingDate = null,
    DateOnly? RegisteredDate = null,
    bool? RegisteredInVatRegistry = null,
    bool? RegisteredInBusinessRegistry = null,
    bool IsBankrupt = false,
    DateOnly? BankruptcyDate = null,
    DateOnly? DeletedDate = null
);

public sealed record PostalAddress(
    string? StreetAddress,
    string? PostalCode,
    string? City,
    string? Municipality,
    string? Country);

public sealed record IndustryCode(string Code, string Description);
