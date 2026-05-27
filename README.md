# Bronnoysund.MVP

MVP delivery for the Brreg Company Lookup home assignment.
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

Inspired by (no code copied — own implementation per the assignment):

- [Frank.Libraries.Brreg](https://github.com/frankhenrichdamgaard/Frank.Libraries) — Brreg lookup patterns
- [organisationsnummer/csharp](https://github.com/organisationsnummer/csharp) — MOD11 reference
- [SindreMA](https://github.com/SindreMA) — Brreg endpoint exploration

## License

[MIT](LICENSE) — © 2026 Erling Svanberg Mytting.
