// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;

namespace Bronnoysund.Infrastructure.Brreg;

/// <summary>
/// JSON contract for the Brreg <c>/enheter/{orgnr}/roller</c> response. Roles arrive grouped
/// into <c>rollegrupper</c> (STYR, DAGL, REVI, …); each group holds individual <c>roller</c>
/// whose holder is either a person or another entity. The projection into the English-field
/// <see cref="Bronnoysund.Application.Dtos.CompanyRolesResponse"/> happens in
/// <see cref="BrregRolesProvider"/>.
/// </summary>
internal sealed class BrregRollerDto
{
    [JsonPropertyName("rollegrupper")]
    public List<BrregRollegruppeDto>? Rollegrupper { get; set; }
}

internal sealed class BrregRollegruppeDto
{
    [JsonPropertyName("type")]
    public BrregKodeDto? Type { get; set; }

    [JsonPropertyName("roller")]
    public List<BrregRolleDto>? Roller { get; set; }
}

internal sealed class BrregRolleDto
{
    [JsonPropertyName("type")]
    public BrregKodeDto? Type { get; set; }

    [JsonPropertyName("person")]
    public BrregRollePersonDto? Person { get; set; }

    [JsonPropertyName("enhet")]
    public BrregRolleEnhetDto? Enhet { get; set; }

    [JsonPropertyName("valgtAv")]
    public BrregKodeDto? ValgtAv { get; set; }

    // Fratraadt is being retired (May 2026) in favour of Avregistrert; honour both while the
    // overlap lasts so a role registered as either is filtered out of the active list.
    [JsonPropertyName("fratraadt")]
    public bool? Fratraadt { get; set; }

    [JsonPropertyName("avregistrert")]
    public bool? Avregistrert { get; set; }

    [JsonPropertyName("rekkefolge")]
    public int? Rekkefolge { get; set; }
}

internal sealed class BrregRollePersonDto
{
    [JsonPropertyName("navn")]
    public BrregRollePersonNavnDto? Navn { get; set; }

    [JsonPropertyName("fodselsdato")]
    public string? Fodselsdato { get; set; }

    [JsonPropertyName("erDoed")]
    public bool? ErDoed { get; set; }
}

internal sealed class BrregRollePersonNavnDto
{
    [JsonPropertyName("fornavn")]
    public string? Fornavn { get; set; }

    [JsonPropertyName("mellomnavn")]
    public string? Mellomnavn { get; set; }

    [JsonPropertyName("etternavn")]
    public string? Etternavn { get; set; }

    /// <summary>Join the name parts present into a single display name.</summary>
    public string? Full()
    {
        var joined = string.Join(' ', new[] { Fornavn, Mellomnavn, Etternavn }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}

internal sealed class BrregRolleEnhetDto
{
    [JsonPropertyName("organisasjonsnummer")]
    public string? Organisasjonsnummer { get; set; }

    // Brreg returns the entity name as a list of lines.
    [JsonPropertyName("navn")]
    public List<string>? Navn { get; set; }

    /// <summary>Join the entity name lines into a single display name.</summary>
    public string? NameLine() =>
        Navn is { Count: > 0 }
            ? string.Join(' ', Navn.Where(l => !string.IsNullOrWhiteSpace(l)))
            : null;
}
