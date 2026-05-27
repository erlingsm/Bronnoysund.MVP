# Bronnoysund.MVP

MVP delivery for the Brreg Company showcasing a simple lookup against the Register.
Given a Norwegian organisation number, looks up the company in the Brønnøysund
Enhetsregisteret and returns a simplified English response. Includes a Blazor
Server web frontend with English / Bokmål / Nynorsk language switching and a
name-search extension.

**Live:**

- UI + JSON API: <https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io>
- WebApi standalone (JSON only): <https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io>

> Sister project (full product family — MAUI Desktop, MAUI Mobile, Azure
> Container Apps, multi-registry aggregator, persistence, watch apps):
> <https://github.com/erlingsm/Bronnoysund.Lookup>

## Stack

- **.NET 10** (10.0.203 or later)
- **ASP.NET Core Minimal API** for the REST endpoint host
- **Blazor Server + MudBlazor** for the web UI
- **CommunityToolkit.Mvvm** for the lookup view-model
- **Polly v8** (`Microsoft.Extensions.Http.Resilience`) for retry / circuit-breaker / timeout
- **HybridCache** for in-process cache with 24 h TTL
- **Serilog** for structured logging (console + rolling file)
- **MSTest + FluentAssertions + NSubstitute + WireMock.Net** for testing

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
| `GET` | _(not exposed)_ | `/companies?name={q}&size={N}&page={P}` | Paginated name search |
| `GET` | _(not exposed)_ | `/health` | Liveness probe |

The BlazorWeb host keeps its surface minimal — the UI uses its own internal handlers and only exposes the orgnr lookup as JSON. The WebApi host is the full REST surface.

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

### Testing the API

**Live** (curl-cookbook):

```bash
# Happy path — orgnr lookup
curl -s https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io/api/companies/919300388 | python3 -m json.tool
curl -s https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io/companies/919300388 | python3 -m json.tool

# Happy path — name search with pagination
curl -s "https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io/companies?name=Statens+vegvesen&size=10&page=0" | python3 -m json.tool

# Invalid input → 400
curl -sw "\nHTTP %{http_code}\n" https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io/companies/12345

# Health
curl -s https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io/health
```

**Locally** — see [Run locally](#run-locally) below for the `dotnet run` commands.

**Curated test inputs** (orgnr that hit each path, search queries that exercise pagination) — see [Demo guide](#demo-guide).

**In the IDE** — open [`src/Bronnoysund.WebApi/Bronnoysund.WebApi.http`](src/Bronnoysund.WebApi/Bronnoysund.WebApi.http) in JetBrains Rider or VS Code with the REST Client extension; each request is one click.

**Automated** — `dotnet test` runs 77 tests against a WireMock-stubbed Brreg, including HTTP-status mapping, MOD11 edge cases, search pagination, and cache round-trips. No live Brreg calls during CI.

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
curl "http://localhost:5000/companies?name=Statens+vegvesen&size=5"
```

Both hosts hit Brønnøysund directly (no DB, no separate API tier — the
Application + Infrastructure layers are shared between them).

## UI features

What the user sees when interacting with the deployed BlazorWeb:

### Lookup mode toggle

Radio buttons at the top of the form switch between **organisation-number lookup** and **name search**. Each mode has its own input + button.

### Paginated name search

When a name search returns more hits than fit on one page, three controls appear under the result table:

- **`Page X of Y · N hits total`** — caption with current page, total pages, and total result count, straight from Brreg's page metadata.
- **`MudPagination`** — page navigator (`< 1 2 3 ... >`). Only shown when `TotalPages > 1`.
- **`Hits per page`** dropdown — `10 / 25 / 50 / 100`. Default 25. Changing the value re-runs the search at page 0 with the new size.

The result list is cached for 5 minutes per `(query, page-size, page)` combination via `HybridCache`. Paging back and forth across pages you've already seen does not hit Brreg.

### JSON flip-view on the detail card

Once a single company is shown (either via orgnr lookup or by drilling into a search hit), a small icon button appears in the **bottom-right corner of the detail card**.

- Click the `<>` icon → the card flips with a 0.6 s `rotateY` animation to show the same data as raw JSON, formatted line-by-line — the exact shape the JSON API returns.
- Click the `👁` icon on the back of the card → flips back to the structured view.

Useful for demoing the API contract without leaving the UI.

### Title click resets the form

Clicking **"Virksomhetsinformasjon fra Brønnøysundregistrene"** (or its English / Nynorsk equivalent) in the app-bar clears every form field, error, status message, search result and pagination state — and navigates the browser back to `/`.

The HybridCache layer in Infrastructure is untouched, so the next search or lookup for something you've already queried is still served from cache.

### Language switcher

Hamburger menu (top-right of app-bar) → choose **English / Norsk / Nynorsk**. Selection is persisted via a cookie set by a `/set-culture` endpoint (closed allowlist + `Results.LocalRedirect` for safety), so the choice survives reloads.

## Demo guide

A curated set of inputs to exercise each path during a live demo.

### Lookup — happy paths (returns 200 with the 4 English fields)

| Orgnr | Company | Notes |
| --- | --- | --- |
| `919300388` | Artisan Consulting AS | Used in tests |
| `974760843` | Riksrevisjonen | Public-sector entity (Bokmål `maalform`) |
| `971032081` | Statens vegvesen | Government body (`ORGL` form) |
| `933722821` | Røa Systemutvikling AS | Small AS |

### Lookup — error paths

| Orgnr / Input | Outcome | HTTP |
| --- | --- | --- |
| `12345` | Too short → "must be exactly 9 digits" | `400` |
| `abc123def` | Non-digit → "can only contain digits" | `400` |
| `712345678` | Wrong prefix → "must start with 8 or 9" | `400` |
| `800000050` | Valid format but `sum % 11 == 1` (MOD11 rejects "check digit would have been 10") | `400` |
| `899999991` | Valid MOD11, but likely **not registered** in Brreg → "Not found" | `404` |
| empty | "cannot be empty" | `400` |

To exercise the timeout / unavailable branch, you'd need to point `Brreg:BaseUrl` at an unreachable host — not part of the live demo, but covered by `BrregHttpClientIntegrationTests.Status500_ThrowsBrregUnavailable`.

### Name search — happy paths

| Query | Expected | Notes |
| --- | --- | --- |
| `Statens vegvesen` | 1–3 hits | Single-result drill-down |
| `Equinor` | 1–5 hits | Large entity, multiple sub-units |
| `Røa` | Many hits | Tests UTF-8 query encoding and pagination |
| `Universitetet` | Many hits | Pagination across multiple pages |

### Name search — guarded paths

| Query | Outcome |
| --- | --- |
| empty / whitespace | "must be at least 2 characters" |
| `a` | Same — single character also blocked |
| `enikkeeksisterendebedrift123` | Empty hit list, "No companies found" message |

### Quick smoke against live

```bash
# UI host returns the 4 required fields + a "details" sub-object with bonus data
curl -s https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io/api/companies/919300388

# Standalone WebApi returns the same JSON, plus name search
curl -s https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io/companies/919300388
curl -s "https://bronnoysund-webapi.redpebble-469bb928.norwayeast.azurecontainerapps.io/companies?name=Statens+vegvesen&size=5"
```

## Run tests

```bash
dotnet test
```

~77 tests across four test projects:

- `Bronnoysund.Domain.Tests` — organisation-number validation (MOD11, normalisation, edge cases)
- `Bronnoysund.Application.Tests` — handler behaviour with mocked port, search-paging, cancellation
- `Bronnoysund.Infrastructure.Tests` — `BrregHttpClient` with WireMock stubs, search, mapping, caching round-trips
- `Bronnoysund.ViewModels.Tests` — localisation parity (en / nb-NO / nn-NO)

Total run time: under 1 s after first build. No real Brønnøysund call needed
for the test suite.

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
| `Application` | Use cases (`LookupCompanyHandler`, `SearchCompaniesByNameHandler`), ports (`ICompanyProvider`, `ICompanySearchProvider`), validators, result discriminated union | Domain only |
| `Infrastructure` | `BrregHttpClient` + DTOs, `CachingCompanyProvider` decorator, Polly resilience, HybridCache | Application + HTTP + caching libs |
| `WebApi` | Minimal API endpoints (`/companies/{orgnr}`, `/companies?name=`, `/health`) | Application + Infrastructure |
| `ViewModels` | `CompanyLookupViewModel` (MVVM), localised strings | Application |
| `Components` | Razor pages (`Lookup`, `Settings`) with MudBlazor | ViewModels + MudBlazor |
| `BlazorWeb` | Server-mode host, cookie-based culture switcher, request localisation | Components + ViewModels + Application + Infrastructure |

## Key design choices

### Validation in Domain

`OrganizationNumber.TryCreate` enforces the 9-digit rule, the 8/9 prefix rule,
and the MOD11 control digit before any HTTP call. The assignment only required
the first two rules; MOD11 is a free quality win that prevents one wasted
roundtrip per invalid input.

### Caching as a decorator

`CachingCompanyProvider` wraps the real `BrregCompanyProvider` and uses
`HybridCache` (L1 in-process) with a 24 h default TTL. The decorator pattern
keeps the caching concern out of the handler and trivially supportable to
swap for a distributed L2 (Redis) later — `HybridCache` supports both layers
behind the same API.

### Resilience via Polly v8

`AddStandardResilienceHandler()` on the typed `HttpClient` gives retry with
jitter, per-attempt timeout, circuit breaker, and rate limiter — all
Microsoft-recommended defaults. On unrecoverable failure
`BrregUnavailableException` surfaces as HTTP 503 from the WebApi and a
"Registry unavailable" alert in the Blazor UI.

### Discriminated union for outcomes

`CompanyLookupResult` is a sealed abstract base with `Found`, `NotFound`,
`InvalidInput`, `Unavailable` subtypes. Pattern-matching maps each to the
right HTTP status (200, 404, 400, 503) — no exception-throwing for control
flow. Business outcomes get a tag, not a stack trace.

### Localisation without a database

The Blazor frontend supports English, Bokmål, and Nynorsk. The language
switcher writes a culture cookie via a minimal `/set-culture` endpoint;
`UseRequestLocalization` reads it on the next request. No database, no
session state, no JS interop — pure ASP.NET Core primitives.

## Deploy

Two Azure Container Apps (region `norwayeast`): **BlazorWeb** (UI + `/api`)
and **WebApi** (JSON only). Both are deployed by a single GitHub Actions
workflow on push to `master`, gated on a green test job. OIDC + Federated
Credential — no client-secret stored anywhere.

See [deploy/README.md](deploy/README.md) for the full pipeline (CI/CD vs.
local manual via `deploy/deploy.sh`, RBAC setup, branch protection).

The architecture is host-agnostic. Application + Infrastructure layers have
zero runtime-specific dependencies; other validated targets are App Service
Linux (when VM quota allows), any glibc 2.31+ Linux behind Nginx/Apache as
a reverse proxy (Blazor SignalR needs WebSocket upgrade headers), or local
`dotnet run`.

## Credits

### Primary sources

Brønnøysundregistrene maintains the authoritative resources this MVP integrates with:

- **Umbrella developer docs**: <https://brreg.github.io/docs/> — canonical hub for the whole Brreg ecosystem (8 registries + Maskinporten + Altinn integration patterns)
- **GitHub org**: <https://github.com/orgs/brreg/repositories> — Brreg's own open-source clients and tooling
- **Enhetsregisteret API documentation** (the one register we currently call): <https://data.brreg.no/enhetsregisteret/api/dokumentasjon/no/index.html>
- **Enhetsregisteret OpenAPI 3 specification**: <https://data.brreg.no/81088a24-8b8d-4b8f-b7be-0b03932bcb91>

The OpenAPI spec is the contract we wrote our `BrregHttpClient` and DTOs against. See the [API integration choices](#api-integration-choices) section for why we hand-rolled the client instead of generating from the spec.

### Secondary inspirations

Third-party C# libraries reviewed for patterns:

- [Frank.Libraries.Brreg](https://github.com/frankhenrichdamgaard/Frank.Libraries) — Brreg lookup patterns
- [organisationsnummer/csharp](https://github.com/organisationsnummer/csharp) — MOD11 reference
- [SindreMA](https://github.com/SindreMA) — Brreg endpoint exploration

## API integration choices

### Why hand-rolled `BrregHttpClient` and not Kiota / NSwag from the OpenAPI spec?

A deliberate trade-off given the scope of this MVP.

| Approach | Lines committed to repo | Covers only what we use |
| --- | --- | --- |
| [Kiota](https://learn.microsoft.com/openapi/kiota/overview) (default) | ~8 000 | ❌ Whole Brreg API (~50 endpoints, ~200 DTOs) |
| [NSwag](https://github.com/RicoSuter/NSwag) (default) | ~3 000 | ❌ Whole Brreg API |
| [Refit](https://github.com/reactiveui/refit) (interface) | ~20 | ✓ Only what we declare |
| **Hand-rolled (chosen)** | ~150 | ✓ Only what we use |

We use **2 endpoints** out of ~50 in Brreg's spec. Code generation would put 4 000+ lines of auto-generated code in the repo for surfaces we never call — every regeneration would be a 4 000-line diff in code review, IDE search would hit `Generated/` files for unrelated endpoints, and a new reader would have to learn which folder bugs do _not_ live in.

Schema drift is caught by the WireMock-stubbed integration tests (`Status404_ReturnsNull`, `Status410Gone_ReturnsNull`, mapping tests) deterministically — we don't need a regenerator to notice when Brreg changes a field.

**When we'd switch**: when the surface grows past ~20 endpoints, or when another team consumes Brreg DTOs as a public contract. For the sister project ([Bronnoysund.Lookup](https://github.com/erlingsm/Bronnoysund.Lookup)) — a multi-registry aggregator that also pulls from Skatteetaten, SSB and others — Kiota would be the right call from day one.

## Future scope

This MVP integrates with a deliberately narrow slice of the Brønnøysund Register Centre — `GET /enheter/{orgnr}` and `GET /enheter?navn=` on Enhetsregisteret. The umbrella docs at <https://brreg.github.io/docs/> describe **eight registries plus Maskinporten and Altinn integration patterns**. Each one would be a new `IXxxProvider` port in `Application/Ports` with a matching adapter in `Infrastructure` — the use-case handlers and UI would not need to change.

### Adjacent extensions inside Enhetsregisteret (no auth needed)

| Endpoint | What it adds | Effort |
| --- | --- | --- |
| `GET /enheter/{orgnr}/roller` | Roles (board, CEO, signature authority) per entity | 1 new DTO + 1 endpoint + UI tab |
| `GET /enheter/{orgnr}/underenheter` | Sub-units / branches | Tree visualisation for conglomerates |
| `GET /enheter/lastet-ned/oppdateringer` | Change feed since timestamp | Event-driven cache invalidation, "what changed for orgnr X since I last looked?" |
| `GET /organisasjonsformer` | Code → description (AS = Aksjeselskap) | Tooltip / human-readable labels |

### Other registries (full system-of-systems integration)

| Registry | Auth | Use case |
| --- | --- | --- |
| **Foretaksregisteret** | None | Verify a company is registered for legal/commercial activity (relevant for AS / ASA / SE) |
| **Reelle rettighetshavere** | Maskinporten | Beneficial-ownership lookups (AML, KYC). Restricted to qualified consumers. |
| **Regnskapsregisteret** | Maskinporten | Pull the latest annual report on demand |
| **Løsøreregisteret** | None | Pledges and liens on vehicles, equipment, chattels |
| **Ektepaktregisteret** | Person-scoped | Prenuptial agreements (rare consumer scenario) |

### What's already designed-in for these extensions

- **Port-and-adapter boundary** — `Application` defines what we need from a registry; `Infrastructure` provides it. Adding a new registry adapter doesn't ripple into the use-case handlers.
- **Caching as a decorator** — the same `HybridCache` pattern (`CachingCompanyProvider`, `CachingCompanySearchProvider`) drops onto any new adapter without modifying the underlying HTTP client.
- **Polly standard resilience handler** — applied per typed `HttpClient`, so each new registry gets retry + circuit breaker + timeout out of the box.

## License

[MIT](LICENSE) — © 2026 Erling Svanberg Mytting.
