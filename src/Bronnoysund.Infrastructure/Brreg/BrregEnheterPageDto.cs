// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;

namespace Bronnoysund.Infrastructure.Brreg;

// Wire DTO for /enheter?navn=... (paginated search). Same HAL shape as underenheter, but
// the embedded array carries full entity records. We only need a subset of those fields
// to render a search-result row (orgnr, name, form, postal city), so a dedicated DTO keeps
// deserialization fast and the surface intentionally narrow.
internal sealed class BrregEnheterPageDto
{
    [JsonPropertyName("_embedded")]
    public BrregEnheterEmbeddedDto? Embedded { get; set; }

    [JsonPropertyName("page")]
    public BrregPageMetaDto? Page { get; set; }
}

internal sealed class BrregEnheterEmbeddedDto
{
    [JsonPropertyName("enheter")]
    public List<BrregEnhetSearchHitDto>? Enheter { get; set; }
}

internal sealed class BrregEnhetSearchHitDto
{
    [JsonPropertyName("organisasjonsnummer")]
    public string? Organisasjonsnummer { get; set; }

    [JsonPropertyName("navn")]
    public string? Navn { get; set; }

    [JsonPropertyName("organisasjonsform")]
    public BrregOrganisasjonsformDto? Organisasjonsform { get; set; }

    [JsonPropertyName("postadresse")]
    public BrregAdresseDto? Postadresse { get; set; }
}

internal sealed class BrregAdresseDto
{
    [JsonPropertyName("poststed")]
    public string? Poststed { get; set; }
}

internal sealed class BrregPageMetaDto
{
    [JsonPropertyName("totalElements")]
    public int TotalElements { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }
}
