// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

using Bronnoysund.Lookup.Domain;
using FluentValidation;

namespace Bronnoysund.Lookup.Application.Validators;

/// <summary>
/// Application-layer validation of an organization-number string before we try to build an
/// <see cref="OrganizationNumber"/>. Provides readable error messages on 9-digit / starts-with-8-or-9 violations.
/// The final MOD11 check is performed in the Value Object itself.
/// </summary>
public sealed class OrganizationNumberValidator : AbstractValidator<string>
{
    public OrganizationNumberValidator()
    {
        RuleFor(x => x)
            .NotEmpty()
            .WithMessage("Organization number cannot be empty.");

        RuleFor(x => x)
            .Must(s => s is not null && s.Where(char.IsDigit).Count() == 9)
            .WithMessage("Organization number must contain exactly 9 digits.");

        RuleFor(x => x)
            .Must(s => s is not null && s.All(c => char.IsDigit(c) || char.IsWhiteSpace(c) || c == '-' || c == '.'))
            .WithMessage("Organization number can only contain digits (optionally with whitespace).");

        RuleFor(x => x)
            .Must(s =>
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    return false;
                }
                var firstDigit = s.FirstOrDefault(char.IsDigit);
                return firstDigit is '8' or '9';
            })
            .WithMessage("Organization number must start with 8 or 9.");
    }
}
