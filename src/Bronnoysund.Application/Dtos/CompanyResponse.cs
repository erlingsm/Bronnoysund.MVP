// SPDX-License-Identifier: MIT

namespace Bronnoysund.Application.Dtos;

/// <summary>
/// English-field response for organization-number lookups, per the original task requirements.
/// The four top-level fields cover the MVP contract exactly as specified; richer information from
/// the open Brreg entity payload (contact info, address, industry, lifecycle dates) is exposed
/// under <see cref="Details"/> so the contract stays tight while UI consumers still get the bonus
/// fields without an extra call.
/// </summary>
public sealed record CompanyResponse(
    string OrganizationNumber,
    string OrganizationName,
    string CompanyType,
    string LanguageForm,
    CompanyDetails? Details = null
);

/// <summary>
/// Optional secondary fields populated from the Brreg entity payload when available.
/// Null defaults so a registry response without these fields still serializes to a sensible
/// CompanyDetails (or to a missing Details on CompanyResponse altogether).
/// </summary>
public sealed record CompanyDetails(
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
