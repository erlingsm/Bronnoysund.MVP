// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;
using Bronnoysund.Domain;

namespace Bronnoysund.Infrastructure.Brreg;

/// <summary>
/// JSON contract for a single entity from the Brreg Enhetsregisteret. Norwegian field names
/// match the /enhetsregisteret/api/enheter/{orgnr} response. The mapping to our domain entity
/// happens in <see cref="ToDomain"/>; the surface-level CompanyResponse projection is built
/// by the calling provider so that extra fields (address, contact, industry) can be added
/// without disturbing the domain model.
/// </summary>
internal sealed class BrregEnhetDto
{
    [JsonPropertyName("organisasjonsnummer")]
    public string? Organisasjonsnummer { get; set; }

    [JsonPropertyName("navn")]
    public string? Navn { get; set; }

    [JsonPropertyName("organisasjonsform")]
    public BrregOrganisasjonsformDto? Organisasjonsform { get; set; }

    [JsonPropertyName("maalform")]
    public string? Maalform { get; set; }

    // Bankruptcy data lives on the entity record itself — Brreg flips `konkurs` to true and
    // populates `konkursdato` when the bankruptcy court has registered the case. There is no
    // separate "konkursregister" REST endpoint per orgnr; the entity record is the source.
    [JsonPropertyName("konkurs")]
    public bool Konkurs { get; set; }

    [JsonPropertyName("konkursdato")]
    public string? Konkursdato { get; set; }

    // Contact info.
    [JsonPropertyName("hjemmeside")]
    public string? Hjemmeside { get; set; }

    [JsonPropertyName("epostadresse")]
    public string? Epostadresse { get; set; }

    [JsonPropertyName("telefon")]
    public string? Telefon { get; set; }

    [JsonPropertyName("mobil")]
    public string? Mobil { get; set; }

    // Addresses. Brreg returns up to three: postal (postadresse), business (forretningsadresse),
    // and location (beliggenhetsadresse). Business is the most commonly meaningful one.
    [JsonPropertyName("forretningsadresse")]
    public BrregFullAdresseDto? Forretningsadresse { get; set; }

    [JsonPropertyName("postadresse")]
    public BrregFullAdresseDto? Postadresse { get; set; }

    // Activity classification.
    [JsonPropertyName("naeringskode1")]
    public BrregKodeDto? Naeringskode1 { get; set; }

    [JsonPropertyName("antallAnsatte")]
    public int? AntallAnsatte { get; set; }

    [JsonPropertyName("harRegistrertAntallAnsatte")]
    public bool? HarRegistrertAntallAnsatte { get; set; }

    [JsonPropertyName("institusjonellSektorkode")]
    public BrregKodeDto? InstitusjonellSektorkode { get; set; }

    // Lifecycle dates and registry presence.
    [JsonPropertyName("stiftelsesdato")]
    public string? Stiftelsesdato { get; set; }

    [JsonPropertyName("registreringsdatoEnhetsregisteret")]
    public string? RegistreringsdatoEnhetsregisteret { get; set; }

    [JsonPropertyName("registrertIMvaregisteret")]
    public bool? RegistrertIMvaregisteret { get; set; }

    [JsonPropertyName("registrertIForetaksregisteret")]
    public bool? RegistrertIForetaksregisteret { get; set; }

    [JsonPropertyName("registrertIStiftelsesregisteret")]
    public bool? RegistrertIStiftelsesregisteret { get; set; }

    [JsonPropertyName("registrertIFrivillighetsregisteret")]
    public bool? RegistrertIFrivillighetsregisteret { get; set; }

    [JsonPropertyName("slettedato")]
    public string? Slettedato { get; set; }

    /// <summary>Map to our core domain entity. Returns null if critical fields are missing.</summary>
    public Company? ToDomain()
    {
        if (string.IsNullOrWhiteSpace(Organisasjonsnummer) || string.IsNullOrWhiteSpace(Navn))
        {
            return null;
        }

        if (!OrganizationNumber.TryCreate(Organisasjonsnummer, out var orgNumber, out _))
        {
            return null;
        }

        var formCode = Organisasjonsform?.Kode ?? "UKJENT";
        var langForm = Maalform switch
        {
            "Bokmål" or "BOKM" or "NB" => LanguageForm.Bokmål,
            "Nynorsk" or "NYNO" or "NN" => LanguageForm.Nynorsk,
            _ => LanguageForm.Unknown,
        };

        return new Company(orgNumber, Navn, formCode, langForm);
    }
}

internal sealed class BrregOrganisasjonsformDto
{
    [JsonPropertyName("kode")]
    public string? Kode { get; set; }

    [JsonPropertyName("beskrivelse")]
    public string? Beskrivelse { get; set; }
}

internal sealed class BrregKodeDto
{
    [JsonPropertyName("kode")]
    public string? Kode { get; set; }

    [JsonPropertyName("beskrivelse")]
    public string? Beskrivelse { get; set; }
}

/// <summary>Full Brreg address with both street + postal + municipality + country.</summary>
internal sealed class BrregFullAdresseDto
{
    [JsonPropertyName("adresse")]
    public List<string>? Adresse { get; set; }

    [JsonPropertyName("postnummer")]
    public string? Postnummer { get; set; }

    [JsonPropertyName("poststed")]
    public string? Poststed { get; set; }

    [JsonPropertyName("kommune")]
    public string? Kommune { get; set; }

    [JsonPropertyName("kommunenummer")]
    public string? Kommunenummer { get; set; }

    [JsonPropertyName("land")]
    public string? Land { get; set; }

    [JsonPropertyName("landkode")]
    public string? Landkode { get; set; }

    /// <summary>Brreg returns street lines as a list; join them with a single space.</summary>
    public string? StreetLine() =>
        Adresse is { Count: > 0 } ? string.Join(' ', Adresse.Where(a => !string.IsNullOrWhiteSpace(a))) : null;
}
