# Bronnoysund.MVP

> Look up Norwegian companies in the Brønnøysund Register Centre. .NET 10 Clean Architecture, Blazor Server UI + JSON REST API, deployed to Azure Container Apps with OIDC-gated CI/CD.

## Background

A small project for self study: Given a Norwegian organisation number, look it up in the Brønnøysund Enhetsregisteret and return a simplified English response. Plus three deliberate extensions agreed up-front — a Blazor Server web frontend with English / Bokmål / Nynorsk language switching, a paginated name search, and a JSON flip-view that lets the user see the API contract from inside the UI.

Sister project (the broader product family — MAUI Desktop, MAUI Mobile, multi-registry aggregator, persistence, watch apps): <https://github.com/erlingsm/Bronnoysund.Lookup>.

## Live

- **UI + JSON API**: <https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io>
- **WebApi standalone** (JSON only): <https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io>

## Demo on the web

A guided click-through of the deployed UI. About three minutes.

1. Open <https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io> in any modern browser. The page is a single input with a "Look up" button and a radio toggle for **Organization number** vs **Name**.

2. **Org-number happy path** — enter `985079056` and press Enter. The detail card slides in with company name (Statoil SP Gas AS), `AS` form code, `Bokmål` language form, plus a "Details" block with industry, employee count, founding date, and which Brreg sub-registries the entity is in. Below that, a **Roles** block lists who's registered in the company — kept in Brreg's own role groups (Daglig leder, Styre, Revisor …), each holder with their role and birth year, and "Elected by" where it applies (e.g. employee representatives). Try `923609016` (Equinor ASA) for a full board. Roles load as a follow-up call right after the core card, so the card never waits on them.

3. **JSON flip-view** — click the small `<>` icon in the bottom-right corner of the detail card. The card flips with a 0.6 s `rotateY` animation to show the same data as JSON, formatted line-by-line — the exact shape the API returns. Click the eye icon on the back of the card to flip it back.

4. **Error path — invalid format** — clear the input, enter `12345`, press Look up. Error message: "Organization number must be exactly 9 digits (got 5)." Validation runs in `Domain` before any HTTP call.

5. **Error path — MOD11 rejection** — enter `800000050`. Error: "Organization number has an invalid MOD11 check digit." This is the edge case where the first 8 digits' weighted sum modulo 11 equals 1 — invalid because the check digit would have had to be 10. The original assignment didn't require MOD11; we added it as a free quality win that saves one wasted HTTP roundtrip per invalid input.

6. **Name search with pagination** — flip the radio to **Name**, enter `Universitetet`, press Search. After a beat, a paginated table appears with up to 25 hits. Below the table: page navigator (`< 1 2 3 ... >`), page-size dropdown (10 / 25 / 50 / 100), and a caption "Page 1 of N · M hits total".

7. **Cache test** — click page 2, then page 3, then back to page 1. The first visit to each page is a Brreg call (~150 ms); revisits within five minutes are served from the in-process `HybridCache` without a network roundtrip.

8. **Drill-down** — click any row in the search table. The detail card for that company appears _above_ the search list (deliberate order — drill-down first, breadcrumb of "Other hits" below).

9. **Title-click reset** — click **"Information about companies from the Brønnøysund Register Centre"** in the app-bar. The form clears, but the `HybridCache` layer is preserved — the next lookup or search for something you've already queried still hits cache.

10. **Language switcher** — hamburger menu (top-right) → choose **Norsk** or **Nynorsk**. Cookie-based, persists across reloads. All labels, error messages and pagination captions are localised via `IStringLocalizer<SharedResources>` against three `.resx` files. Parity tests enforce that every key in the default `.resx` exists in `nb-NO` and `nn-NO` — no silent English fallback.

11. **JSON API direct** — in a terminal:

    ```bash
    curl -s https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io/api/companies/919300388 | python3 -m json.tool
    ```

    Same shape as the JSON flip-view shows.

For a curated table of inputs that hit every code path, see [Demo inputs](#demo-inputs).

## Stack

- **.NET 10** (10.0.203 or later)
- **ASP.NET Core Minimal API** for the REST endpoint host
- **OpenAPI (built-in `Microsoft.AspNetCore.OpenApi`) + Swagger UI** — interactive docs at `/swagger`, machine-readable contract at `/openapi/v1.json`
- **Blazor Server + MudBlazor** for the web UI
- **CommunityToolkit.Mvvm** for the lookup view-model
- **Polly v8** (`Microsoft.Extensions.Http.Resilience`) — retry / circuit breaker / timeout / rate limiter
- **HybridCache** — in-process cache; 24 h TTL for orgnr lookups, 5 min for name-search pages
- **Serilog** — structured logging (console + rolling file)
- **MSTest + FluentAssertions + NSubstitute + WireMock.Net** — testing (83 tests, < 1 s total runtime)

## Project layout

```text
/
├── src/
│   ├── Bronnoysund.Domain/             OrganizationNumber value object, Company, LanguageForm enum
│   ├── Bronnoysund.Application/        Use-case handlers, ports, result discriminated union, DTOs
│   ├── Bronnoysund.Infrastructure/     BrregHttpClient + DTOs, caching decorators, Polly setup
│   ├── Bronnoysund.WebApi/             Minimal API host (JSON only)
│   ├── Bronnoysund.ViewModels/         CompanyLookupViewModel + localisation .resx
│   ├── Bronnoysund.Components/         Razor components (Lookup page) — RCL shared between hosts
│   └── Bronnoysund.BlazorWeb/          Blazor Server host (UI + /api JSON endpoint)
├── tests/
│   ├── Bronnoysund.Domain.Tests/         24 tests — MOD11, normalisation, edge cases
│   ├── Bronnoysund.Application.Tests/    21 tests — handler logic, pagination, cancellation
│   ├── Bronnoysund.Infrastructure.Tests/ 30 tests — WireMock-stubbed HTTP + cache round-trips/eviction, roles mapping
│   └── Bronnoysund.ViewModels.Tests/      8 tests — localisation parity (en / nb-NO / nn-NO)
├── deploy/
│   ├── deploy.sh                       Local manual deploy (build + test + acr build + update)
│   ├── teardown.sh                     az group delete with --yes guard
│   └── README.md                       CI/CD vs local, RBAC + managed identity setup
└── .github/workflows/
    └── build-and-deploy.yml            OIDC-gated CI/CD: parallel deploy to BlazorWeb + WebApi
```

## API

Two hosts expose the same JSON API. Same handler, same caching, same response shape — different paths and one has a UI:

| Host | Base URL | UI? | JSON path prefix |
| --- | --- | --- | --- |
| **BlazorWeb** | <https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io> | Yes | `/api` |
| **WebApi** | <https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io> | No | _(root)_ |

### Endpoints

| Method | Path (BlazorWeb) | Path (WebApi) | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/companies/{orgnr}` | `/companies/{orgnr}` | Look up by organisation number |
| `GET` | `/api/companies/{orgnr}/roles` | `/companies/{orgnr}/roles` | Roles (board, CEO, auditor …) grouped by role group |
| `GET` | _(not exposed)_ | `/companies?name={q}&size={N}&page={P}` | Paginated name search |
| `GET` | _(not exposed)_ | `/health` | Liveness probe |
| `GET` | `/openapi/v1.json` | `/openapi/v1.json` | OpenAPI 3 document |
| `GET` | `/swagger` | `/swagger` | Swagger UI (interactive docs) |

The BlazorWeb host keeps its surface minimal — the UI uses its own internal handlers and exposes the orgnr lookup and its roles as JSON. The WebApi host is the full REST surface.

### API docs (OpenAPI / Swagger)

Both hosts publish their JSON surface as an OpenAPI 3 document via the .NET 10 built-in generator (`Microsoft.AspNetCore.OpenApi`), with Swagger UI on top:

| Host | Swagger UI | OpenAPI document |
| --- | --- | --- |
| **BlazorWeb** | <https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io/swagger> | `/openapi/v1.json` |
| **WebApi** | <https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io/swagger> | `/openapi/v1.json` |

The schemas are generated from each endpoint's response-type metadata (`.Produces<CompanyResponse>()`, `CompanyRolesResponse`, `CompanySearchResult`), so what Swagger shows matches what the API returns; the internal `/set-culture` cookie endpoint is excluded from the document. The docs are served in every environment on purpose — this is a public demo whose JSON contract is the point.

This MVP is a thin English-field facade over the upstream **Brønnøysund Enhetsregisteret API**, whose own reference documentation lives at <https://data.brreg.no/enhetsregisteret/api/dokumentasjon/no/index.html>.

### Response — `GET /companies/{orgnr}` (200 OK)

The four PDF-required fields at the top, optional `details` sub-object with bonus fields from the Brreg payload:

```json
{
  "organizationNumber": "919300388",
  "organizationName": "ARTISAN CONSULTING AS",
  "companyType": "AS",
  "languageForm": "Bokmål",
  "details": {
    "businessAddress": {
      "streetAddress": "Brynsveien 12",
      "postalCode": "0667",
      "city": "OSLO",
      "municipality": "OSLO",
      "country": "Norge"
    },
    "primaryIndustry": { "code": "62.100", "description": "Dataprogrammeringstjenester" },
    "employeeCount": 11,
    "sectorCode": "2100",
    "sectorDescription": "Private aksjeselskaper mv.",
    "foundingDate": "2017-07-17",
    "registeredDate": "2017-07-17",
    "registeredInVatRegistry": true,
    "registeredInBusinessRegistry": true,
    "isBankrupt": false
  }
}
```

Null fields (`website`, `email`, etc.) are omitted from the serialised JSON (`DefaultIgnoreCondition = WhenWritingNull`).

### Response — `GET /companies/{orgnr}/roles` (200 OK)

Roles are kept in Brreg's own role groups rather than flattened, so the consumer sees the same structure the registry uses. A holder is either a person (carries `dateOfBirth`) or an entity such as an audit firm (carries `organizationNumber`). Roles that are resigned (`fratraadt`) or deregistered (`avregistrert`) are filtered out, and groups left empty after that are dropped. Only open data — names and birth dates — is exposed; never `fødselsnummer` (that requires Maskinporten).

```json
{
  "organizationNumber": "923609016",
  "groups": [
    {
      "typeCode": "DAGL",
      "typeDescription": "Daglig leder",
      "roles": [
        { "roleTypeCode": "DAGL", "roleTypeDescription": "Daglig leder", "name": "Anders Opedal", "dateOfBirth": "1968-05-04", "isDeceased": false }
      ]
    },
    {
      "typeCode": "STYR",
      "typeDescription": "Styre",
      "roles": [
        { "roleTypeCode": "LEDE", "roleTypeDescription": "Styrets leder", "name": "Jon Erik Reinhardsen", "dateOfBirth": "1956-11-30", "isDeceased": false },
        { "roleTypeCode": "MEDL", "roleTypeDescription": "Styremedlem", "name": "Hilde Møllerstad", "isDeceased": false, "electedBy": "Representant for de ansatte" }
      ]
    },
    {
      "typeCode": "REVI",
      "typeDescription": "Revisor",
      "roles": [
        { "roleTypeCode": "REVI", "roleTypeDescription": "Ansvarlig revisor", "name": "ERNST & YOUNG AS", "organizationNumber": "967611600", "isDeceased": false }
      ]
    }
  ]
}
```

A `404` (`not_found`) means Brreg has no roles registered for that orgnr; a valid-but-unregistered or malformed orgnr returns `400` as for the lookup endpoint.

### Response — `GET /companies?name=...` (200 OK)

```json
{
  "hits": [
    { "organizationNumber": "971032081", "name": "STATENS VEGVESEN", "organizationFormCode": "ORGL", "postalCity": "LILLEHAMMER" },
    { "organizationNumber": "987659025", "name": "TEKNA STATENS VEGVESEN", "organizationFormCode": "FLI", "postalCity": "OSLO" }
  ],
  "totalElements": 142,
  "page": 0,
  "totalPages": 6,
  "pageSize": 25
}
```

Default page size is 25, configurable per request via `?size=10|25|50|100`. Page index is 0-based (`?page=0` is the first page). The search cache keeps each `(query, size, page)` combination warm for 5 minutes so paging within a session is free of Brreg roundtrips.

### Status codes

| Code | Meaning | Body |
| --- | --- | --- |
| `200` | Found / OK | The response objects above |
| `400` | Invalid input (validation failed) | `{ "error": "invalid_input", "message": "Organization number must be exactly 9 digits (got 5)." }` |
| `404` | Unknown orgnr (Brreg returned 404 or 410 "Gone") | `{ "error": "not_found", "message": "No company with organization number 919300389 was found." }` |
| `503` | Brreg unreachable / timed out / 5xx after Polly retries | `ProblemDetails` JSON with `title` + `detail` |
| `500` | Unexpected internal error | `ProblemDetails` |

### Demo inputs

Curated test inputs that exercise each code path. Use these for the live demo or for `curl` against either host:

**Lookup**:

| Orgnr / Input | Outcome | HTTP |
| --- | --- | --- |
| `985079056` | Statoil SP Gas AS — happy path | `200` |
| `974760843` | Riksrevisjonen — public-sector entity (Bokmål `maalform`) | `200` |
| `971032081` | Statens vegvesen — `ORGL` organisation form | `200` |
| `915839517` | Programmere AS — small AS | `200` |
| `12345` | Too short → "must be exactly 9 digits" | `400` |
| `abc123def` | Non-digit → "can only contain digits" | `400` |
| `712345678` | Wrong prefix → "must start with 8 or 9" | `400` |
| `800000050` | Valid format but `sum % 11 == 1` (MOD11 edge case) | `400` |
| `899999991` | Valid MOD11, likely **not registered** in Brreg | `404` |
| empty / `""` | "cannot be empty" | `400` |

**Name search**:

| Query | Expected | Notes |
| --- | --- | --- |
| `Statens vegvesen` | 1–3 hits | Single-result drill-down |
| `Equinor` | 1–5 hits | Large entity, multiple sub-units |
| `Røa` | Many hits | Exercises UTF-8 query encoding |
| `Universitetet` | Many hits | Multi-page pagination |
| `a` (single char) | "must be at least 2 characters" | `400` |
| `enikkeeksisterendebedrift123` | Empty hit list, "No companies found" | `200` |

To exercise the `503` timeout / unavailable branch, point `Brreg:BaseUrl` at an unreachable host. Not part of the live demo, but covered by `BrregHttpClientIntegrationTests.Status500_ThrowsBrregUnavailable`.

## Run locally

```bash
# 1. Restore + build (~5 s after first NuGet pull)
dotnet build

# 2. Run the Blazor Web frontend (UI + JSON API combined)
dotnet run --project src/Bronnoysund.BlazorWeb --urls http://localhost:5199
# Then open http://localhost:5199/ in a browser, or call the JSON API:
curl http://localhost:5199/api/companies/919300388

# 3. Or run the standalone REST WebApi (no UI, just JSON)
dotnet run --project src/Bronnoysund.WebApi --urls http://localhost:5000
curl http://localhost:5000/health
curl http://localhost:5000/companies/919300388
curl http://localhost:5000/companies/923609016/roles
curl "http://localhost:5000/companies?name=Statens+vegvesen&size=5"
# Interactive API docs: open http://localhost:5000/swagger (or /openapi/v1.json for the raw doc)
```

Both hosts hit Brønnøysund directly (no DB, no separate API tier — the Application + Infrastructure layers are shared between them).

**In the IDE** — open [`src/Bronnoysund.WebApi/Bronnoysund.WebApi.http`](src/Bronnoysund.WebApi/Bronnoysund.WebApi.http) in JetBrains Rider or VS Code (with the REST Client extension); each request is one click.

## Run tests

```bash
dotnet test
```

83 tests across four projects, total runtime under one second. No real Brønnøysund call during CI — integration tests use WireMock.Net to stub the HTTP layer deterministically.

## Architecture

Clean Architecture + Ports & Adapters. Dependencies point inward:

```text
Domain ← Application ← Infrastructure
              ↑              ↑
              └──── WebApi ──┘
              ↑
              ViewModels ← Components ← BlazorWeb
```

| Layer | Responsibility | Outside dependencies |
| --- | --- | --- |
| `Domain` | `OrganizationNumber` value object, `Company` entity, `LanguageForm` enum | None (.NET BCL only) |
| `Application` | Use cases (`LookupCompanyHandler`, `LookupCompanyRolesHandler`, `SearchCompaniesByNameHandler`), ports (`ICompanyProvider`, `IRolesProvider`, `ICompanySearchProvider`), result discriminated unions | Domain only |
| `Infrastructure` | `BrregHttpClient` + DTOs, `CachingCompanyProvider` / `CachingRolesProvider` decorators, Polly resilience, HybridCache | Application + HTTP + caching libs |
| `WebApi` | Minimal API endpoints (`/companies/{orgnr}`, `/companies/{orgnr}/roles`, `/companies?name=`, `/health`) | Application + Infrastructure |
| `ViewModels` | `CompanyLookupViewModel` (MVVM), localised strings | Application |
| `Components` | Razor pages (`Lookup`) with MudBlazor | ViewModels + MudBlazor |
| `BlazorWeb` | Server-mode host, cookie-based culture switcher, request localisation, `/api/companies/{orgnr}` + `/roles` JSON endpoints | Components + ViewModels + Application + Infrastructure |

### Key design choices

**Validation in Domain.** `OrganizationNumber.TryCreate` enforces the 9-digit rule, the 8/9 prefix rule, and the MOD11 control digit before any HTTP call. The assignment only required the first two rules; MOD11 is a free quality win that prevents one wasted roundtrip per invalid input.

**Caching as a decorator.** `CachingCompanyProvider` and `CachingRolesProvider` wrap the real Brreg providers and use `HybridCache` (L1 in-process) with a 24 h default TTL; `CachingCompanySearchProvider` mirrors the pattern with a 5 min TTL keyed by `(query, page-size, page)`. All three route through one `BrregCache.GetOrCreateUnionAsync` helper that also owns the negative-result policy: a transient `Unavailable` is evicted immediately rather than pinned for the full TTL, so a brief outage never hides a company (or its roles) for 24 h — the Polly circuit breaker guards the repeat traffic. The decorator pattern keeps caching out of the handlers and trivially supports a distributed L2 (Redis) later behind the same `HybridCache` API.

**Resilience via Polly v8.** `AddStandardResilienceHandler()` on the typed `HttpClient` gives retry with jitter, per-attempt timeout, circuit breaker, and rate limiter — all Microsoft-recommended defaults. On unrecoverable failure `BrregUnavailableException` surfaces as HTTP 503 from the WebApi and a "Registry unavailable" alert in the Blazor UI.

**Discriminated union for outcomes.** `CompanyLookupResult` is a sealed abstract base with `Found`, `NotFound`, `InvalidInput`, `Unavailable` subtypes. Pattern-matching maps each to the right HTTP status (200, 404, 400, 503) — no exception-throwing for control flow. Business outcomes get a tag, not a stack trace.

**Localisation without a database.** The Blazor frontend supports English, Bokmål, and Nynorsk. The language switcher writes a culture cookie via a minimal `/set-culture` endpoint; `UseRequestLocalization` reads it on the next request. No database, no session state, no JS interop — pure ASP.NET Core primitives.

**Shared view-model across host + layout.** `CompanyLookupViewModel` is registered as Scoped (per Blazor Server circuit) so the layout (`MainLayout`) and the page (`Lookup`) share the same instance. That's what lets the title-click handler call `VM.Reset()` and see the page update immediately, without re-navigation.

**Roles as separate progressive enrichment.** Roles come from a second Brreg resource (`/enheter/{orgnr}/roller`), so they get their own port, provider, caching decorator (`roles:{orgnr}`, 24 h TTL) and result union rather than being folded into the company lookup. The view-model fetches them as a follow-up right after a successful lookup — the core card renders first, the roles block fills in when ready — and a roles outage never demotes a successful lookup to an error; the section simply stays hidden. The provider keeps Brreg's role groups intact (Styre, Daglig leder, Revisor …) instead of flattening, so the UI presents roles the way the registry organises them. Share ownership (aksjonærer / reelle rettighetshavere) is deliberately not attempted: it has no open API and requires Maskinporten — see [Other Brreg registries](#other-brreg-registries-system-of-systems).

## Brreg coverage

The Brønnøysund Register Centre exposes ~50 endpoints across eight registries on the umbrella docs at <https://brreg.github.io/docs/>. This MVP uses three of them. The architecture is set up so each new endpoint is one adapter in `Infrastructure` with no ripple into the use-case handlers or UI.

### Used today (`Enhetsregisteret`, no auth)

| Endpoint | In code? | In UI? |
| --- | --- | --- |
| `GET /enheter/{orgnr}` | ✓ via `BrregCompanyProvider` | ✓ Lookup page, orgnr mode |
| `GET /enheter?navn=...` (paginated) | ✓ via `BrregCompanySearchProvider` | ✓ Lookup page, name mode |
| `GET /enheter/{orgnr}/roller` | ✓ via `BrregRolesProvider` | ✓ Roles block on the detail card |

### Adjacent extensions inside `Enhetsregisteret` (also no auth)

| Endpoint | What it adds | Effort |
| --- | --- | --- |
| `GET /enheter/{orgnr}/underenheter` | Sub-units / branches | Tree visualisation for conglomerates |
| `GET /enheter/lastet-ned/oppdateringer` | Change feed since timestamp | Event-driven cache invalidation |
| `GET /organisasjonsformer` | Code → description (AS = Aksjeselskap) | Tooltip / human-readable labels |

### Other Brreg registries (system-of-systems)

| Registry | Auth | Use case |
| --- | --- | --- |
| **Foretaksregisteret** | None | Verify a company is registered for legal/commercial activity |
| **Reelle rettighetshavere** | Maskinporten | Beneficial-ownership lookups (AML, KYC). Restricted access. |
| **Regnskapsregisteret** | Maskinporten | Pull the latest annual report on demand |
| **Løsøreregisteret** | None | Pledges and liens on vehicles, equipment, chattels |
| **Ektepaktregisteret** | Person-scoped | Prenuptial agreements (rare consumer scenario) |

The sister project [Bronnoysund.Lookup](https://github.com/erlingsm/Bronnoysund.Lookup) is the natural home for this multi-registry expansion. This MVP keeps the surface tight to the assignment.

## API integration choices

### Why hand-coded `BrregHttpClient` and not Kiota / NSwag from the OpenAPI spec?

A deliberate trade-off given the scope of this MVP.

| Approach | Lines committed to repo | Covers only what we use |
| --- | --- | --- |
| [Kiota](https://learn.microsoft.com/openapi/kiota/overview) (default) | ~8 000 | ❌ Whole Brreg API (~50 endpoints, ~200 DTOs) |
| [NSwag](https://github.com/RicoSuter/NSwag) (default) | ~3 000 | ❌ Whole Brreg API |
| [Refit](https://github.com/reactiveui/refit) (interface) | ~20 | ✓ Only what we declare |
| **Hand-coded (chosen)** | ~150 | ✓ Only what we use |

We use **3 endpoints** out of ~50 in Brreg's spec. Code generation would put 4 000+ lines of auto-generated code in the repo for surfaces we never call — every regeneration would be a 4 000-line diff in code review, IDE search would hit `Generated/` files for unrelated endpoints, and a new reader would have to learn which folder bugs do _not_ live in.

Schema drift is caught by the WireMock-stubbed integration tests (`Status404_ReturnsNull`, `Status410Gone_ReturnsNull`, mapping tests) deterministically — we don't need a regenerator to notice when Brreg changes a field.

**When we'd switch**: when the surface grows past ~20 endpoints, or when another team consumes Brreg DTOs as a public contract. For the sister project ([Bronnoysund.Lookup](https://github.com/erlingsm/Bronnoysund.Lookup)) — a multi-registry aggregator that also pulls from Skatteetaten, SSB and others — Kiota would be the right call from day one.

## Deploy

Two Azure Container Apps (region `norwayeast`): **BlazorWeb** (UI + `/api`) and **WebApi** (JSON only). Both are deployed by a single GitHub Actions workflow on push to `master`, gated on a green test job. OIDC + Federated Credential — no client-secret stored anywhere.

See [deploy/README.md](deploy/README.md) for the full pipeline (CI/CD vs. local manual via `deploy/deploy.sh`, RBAC setup, branch protection).

The architecture is host-agnostic. Application + Infrastructure layers have zero runtime-specific dependencies; other validated targets are Azure App Service Linux (when VM quota allows), any glibc 2.31+ Linux behind Nginx/Apache as a reverse proxy (Blazor SignalR needs WebSocket upgrade headers), or local `dotnet run`.

## Credits

### Primary sources

Brønnøysundregistrene maintains the authoritative resources this MVP integrates with:

- **Umbrella developer docs**: <https://brreg.github.io/docs/> — canonical hub for the whole Brreg ecosystem (8 registries + Maskinporten + Altinn integration patterns)
- **GitHub org**: <https://github.com/orgs/brreg/repositories> — Brreg's own open-source clients and tooling
- **Enhetsregisteret API documentation** (the one register we currently call): <https://data.brreg.no/enhetsregisteret/api/dokumentasjon/no/index.html>
- **Enhetsregisteret OpenAPI 3 specification**: <https://data.brreg.no/81088a24-8b8d-4b8f-b7be-0b03932bcb91>

Data from Brønnøysundregistrene is licensed under [NLOD 2.0](https://data.norge.no/nlod/).

### Secondary inspirations

Third-party C# libraries reviewed for patterns (no code copied — own implementation per the assignment):

- [Frank.Libraries.Brreg](https://github.com/frankhenrichdamgaard/Frank.Libraries) — Brreg lookup patterns
- [organisationsnummer/csharp](https://github.com/organisationsnummer/csharp) — MOD11 reference
- [SindreMA](https://github.com/SindreMA) — Brreg endpoint exploration

## License

[MIT](LICENSE) — © 2026 Erling Svanberg Mytting.
