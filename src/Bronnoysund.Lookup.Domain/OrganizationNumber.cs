// SPDX-License-Identifier: AGPL-3.0-or-later OR LicenseRef-Commercial

namespace Bronnoysund.Lookup.Domain;

/// <summary>
/// Norwegian organization number as a DDD value object. Self-validating: an existing
/// instance is always a valid 9-digit org. number that passes the MOD11 check and starts with 8 or 9.
/// </summary>
/// <remarks>
/// The MOD11 formula is described by Brønnøysundregistrene:
/// https://www.brreg.no/om-oss/registrene-vare/om-enhetsregisteret/organisasjonsnummeret/
/// Weights [3,2,7,6,5,4,3,2] are applied to digits 1-8 from the left, sum mod 11 gives the check digit (digit 9) = 11 - remainder.
/// If remainder = 0 -> check digit = 0. If remainder = 1 -> the org. number is invalid (the check digit would have been 10).
/// </remarks>
public readonly record struct OrganizationNumber
{
    private static readonly int[] Mod11Weights = [3, 2, 7, 6, 5, 4, 3, 2];

    /// <summary>Normalized form: 9 digits with no whitespace or separators.</summary>
    public string Value { get; }

    private OrganizationNumber(string value) => Value = value;

    /// <summary>
    /// Try to build an organization number. Returns false on invalid input and sets an error description.
    /// </summary>
    public static bool TryCreate(string? raw, out OrganizationNumber value, out string? error)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Organization number cannot be empty.";
            return false;
        }

        var normalized = Normalize(raw);

        if (normalized.Length != 9)
        {
            error = $"Organization number must be exactly 9 digits (got {normalized.Length}).";
            return false;
        }

        if (!normalized.All(char.IsDigit))
        {
            error = "Organization number can only contain digits.";
            return false;
        }

        if (normalized[0] != '8' && normalized[0] != '9')
        {
            error = "Organization number must start with 8 or 9.";
            return false;
        }

        if (!IsValidMod11(normalized))
        {
            error = "Organization number has an invalid MOD11 check digit.";
            return false;
        }

        value = new OrganizationNumber(normalized);
        error = null;
        return true;
    }

    /// <summary>Build an organization number or throw <see cref="ArgumentException"/> on invalid input.</summary>
    public static OrganizationNumber Create(string raw)
    {
        if (!TryCreate(raw, out var value, out var error))
        {
            throw new ArgumentException(error, nameof(raw));
        }
        return value;
    }

    /// <summary>Removes whitespace and common separators (space, hyphen, period).</summary>
    private static string Normalize(string raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsWhiteSpace(c) || c == '-' || c == '.')
            {
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static bool IsValidMod11(string nineDigits)
    {
        var sum = 0;
        for (var i = 0; i < 8; i++)
        {
            sum += (nineDigits[i] - '0') * Mod11Weights[i];
        }
        var remainder = sum % 11;
        if (remainder == 1)
        {
            return false; // Check digit would have been 10 — invalid
        }
        var expectedCheckDigit = remainder == 0 ? 0 : 11 - remainder;
        var actualCheckDigit = nineDigits[8] - '0';
        return expectedCheckDigit == actualCheckDigit;
    }

    public override string ToString() => Value;
}
