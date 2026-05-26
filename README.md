# Bronnoysund.Lookup.MVP

MVP delivery for the Brreg Company Lookup home assignment.
Given a Norwegian organisation number, looks up the company in the Brønnøysund
Enhetsregisteret and returns a simplified English response. Includes a Blazor
Server web frontend with English / Bokmål / Nynorsk language switching and a
name-search extension.

**Live demo:** <https://bronnoysund-mvp.redpebble-469bb928.norwayeast.azurecontainerapps.io>

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
- **FluentValidation** for input validation
- **Serilog** for structured logging (console + rolling file)
- **xUnit + FluentAssertions + NSubstitute + WireMock.Net** for testing

## Run locally

```bash
# 1. Restore + build (~5 s after first NuGet pull)
dotnet build

# 2. Run the Blazor Web frontend (primary demo target)
dotnet run --project src/Bronnoysund.Lookup.BlazorWeb --urls http://localhost:5199
# Then open http://localhost:5199/ in a browser.
# Try: orgnr 919300388, or name "Statens vegvesen".
# Switch language at /settings.

# 3. Or run the REST WebApi (alternative target)
dotnet run --project src/Bronnoysund.Lookup.WebApi --urls http://localhost:5000
# Then:
curl http://localhost:5000/health
curl http://localhost:5000/companies/919300388
curl "http://localhost:5000/companies?name=Statens+vegvesen&size=5"
```

Both hosts hit Brønnøysund directly (no DB, no separate API tier — the
Application + Infrastructure layers are shared between them).

## Run tests

```bash
dotnet test
```

~58 tests across four test projects:

- `Bronnoysund.Lookup.Domain.Tests` — organisation-number validation (MOD11, normalisation, edge cases)
- `Bronnoysund.Lookup.Application.Tests` — handler behaviour with mocked port
- `Bronnoysund.Lookup.Infrastructure.Tests` — `BrregHttpClient` with WireMock stubs, search, mapping
- `Bronnoysund.Lookup.ViewModels.Tests` — localisation parity (en / nb-NO / nn-NO)

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

The live demo runs on **Azure Container Apps** (region `norwayeast`):

```bash
# Build image to ACR (no Docker daemon required locally)
az acr build --registry <acrName> --image bronnoysund-mvp:latest \
    --file src/Bronnoysund.Lookup.BlazorWeb/Dockerfile .

# Deploy to Container App
az containerapp update --name bronnoysund-mvp -g bronnoysund-mvp-rg \
    --image <acrName>.azurecr.io/bronnoysund-mvp:latest
```

The architecture is host-agnostic. Other validated targets:

- **Azure App Service Linux** with `az webapp deploy` (when subscription
  quota allows VM-backed plans)
- **Any Linux distro with glibc 2.31+** (Ubuntu 22.04/24.04, Debian 11/12,
  RHEL 9/10) behind Nginx 1.22 or Apache 2.4 as a reverse proxy — requires
  WebSocket upgrade headers for Blazor Server SignalR
- **Local `dotnet run`** for development

The Application and Infrastructure layers have zero dependencies on any
specific runtime.

## Credits

Inspired by (no code copied — own implementation per the assignment):

- [Frank.Libraries.Brreg](https://github.com/frankhenrichdamgaard/Frank.Libraries) — Brreg lookup patterns
- [organisationsnummer/csharp](https://github.com/organisationsnummer/csharp) — MOD11 reference
- [SindreMA](https://github.com/SindreMA) — Brreg endpoint exploration

## License

Dual-licensed:

- [AGPL-3.0-or-later](LICENSE) (open-source)
- [Commercial license from Røa Systemutvikling AS](COMMERCIAL-LICENSE.md) (org.nr 933722821)
