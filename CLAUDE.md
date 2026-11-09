# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

ResidencyRoll tracks international travel and calculates days spent in each country over a rolling 365-day window for residency and tax purposes. Target framework is .NET 10. The repo is hosted on GitHub (`GlenConway/ResidencyRoll`); branch work goes into `dev`, and `dev` merges to `main` through pull requests.

## Solution layout

```
src/
├── ResidencyRoll.Api/      ASP.NET Core Web API: EF Core + SQLite, residency engine, itinerary parsing
│   ├── Controllers/        TripsController (/api/v1/trips), SystemController (/api/v1/system/info)
│   ├── Mcp/                Streamable HTTP MCP server at /mcp (tools, OAuth metadata, guards)
│   ├── Services/           TripService, ResidencyCalculationService, ResidencyQueryService, AirportCatalog, ItineraryParsingService
│   ├── Models/             Trip, DailyPresence, CountryResidencyRule, ResidencyRuleType
│   ├── Configuration/      OpenAIOptions, ConfigureSwaggerOptions
│   └── Migrations/         EF Core migrations
├── ResidencyRoll.Web/      BFF and static host for the React SPA
│   ├── Bff/                YARP /api and /mcp proxy, cookie auth, CSRF, /bff/user, /bff/mcp-info
│   └── ClientApp/          Vite + React 19 + TypeScript UI (Carbon)
├── ResidencyRoll.Shared/   DTOs (Trips/), airport JSON (Airports/airports.json), MCP config helpers, Extensions (ForwardedHeadersExtensions)
├── ResidencyRoll.Tests/    xUnit tests
└── ResidencyRoll.sln
```

The Web app has no database. All data access goes through the API with the signed-in user's access token.

## Commands

```bash
# Build and test (solution file is in src/)
dotnet build src/ResidencyRoll.sln
dotnet test src/ResidencyRoll.sln

# Run locally (API https://localhost:5003, Web https://localhost:5113, UI http://127.0.0.1:5173)
dotnet run --project src/ResidencyRoll.Api
dotnet run --project src/ResidencyRoll.Web
npm run dev --prefix src/ResidencyRoll.Web/ClientApp

# Docker (images pulled from the registry)
docker compose up -d
# Build images locally
docker compose -f docker-compose.build.yml up -d --build
```

Local settings: copy `appsettings.Development.json.example` to `appsettings.Development.json` in each project (git-ignored), or use `dotnet user-secrets`. Docker settings come from `.env` (template: `.env.example`). SQLite files live in `./data` locally and `/app/data` in Docker.

## Architecture

- **Residency engine** (`ResidencyCalculationService`): trips are legs with departure/arrival airports and local times. They become per-day `DailyPresence` records (location at midnight, locations during the day, in-transit flag). Gaps between trips are filled with the last arrival country. Country rules (Midnight vs Partial Day, 183-day threshold) live in a dictionary in the service. See `docs/guides/RESIDENCY_MIDNIGHT_LOGIC.md` and `docs/guides/GAP_FILLING_RULES.md`.
- **Timezones:** all leg maths uses IANA timezones resolved from IATA codes. Check midnight in both departure and arrival zones (International Date Line cases).
- **Auth:** the browser signs in through the Web BFF (OpenID Connect cookie, Auth0 in the reference setup). Tokens stay on the server. The API validates JWT bearer tokens and scopes trips to the user. `Jwt:ClientSecret` enables decryption of JWE tokens. With OIDC disabled, `/bff/user` returns a local user and `/api` is proxied without a bearer token.
- **Itinerary parsing:** Semantic Kernel with OpenAI, configured through `OpenAIOptions`. Optional feature.
- **Home country** is stored in browser localStorage and drives days left/over and timeline emphasis. MCP tools take an optional `homeCountry` and tell the model to ask.
- **MCP:** the API hosts a stateless Streamable HTTP server at `/mcp` (`ModelContextProtocol.AspNetCore` 2.4.0). The Web host proxies it without attaching the cookie token and without a CSRF check. With JWT off, `/mcp` runs as the local user. With JWT on, clients use OAuth 2.1 (Auth0) and the `read:trips` / `write:trips` scopes. See `docs/guides/MCP_SETUP.md` and ADR-016. Personal access tokens (M10.8) are not implemented.
- **Reverse proxy:** `UseConfiguredForwardedHeaders()` (Shared) is used by both hosts.

## Conventions

- **React UI:** pages and components live in `src/ResidencyRoll.Web/ClientApp`. TypeScript is strict. Component logic that must match the old residency rules belongs in `src/lib` with Vitest tests. Details in `.github/copilot-instructions.md`.
- **Tests:** xUnit with plain `Assert` (no Shouldly or Moq packages are referenced). Name tests `MethodName_Condition_ShouldExpectedBehavior`. Tests live in `src/ResidencyRoll.Tests/`. UI tests are Vitest; the Playwright smoke runs against the composed web image.
- **EF migrations:** generate with `dotnet ef`. Do not hand-edit migration files or `ApplicationDbContextModelSnapshot.cs`.
- **Secrets:** never commit client secrets, API keys or `appsettings.Development.json`.
- **Commits:** conventional style (`feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`).

## Documentation

All docs live under `docs/`. See `docs/README.md`.

| Folder | Contents |
|---|---|
| `docs/adr/` | Architecture Decision Records. Read the relevant ones before significant design changes. |
| `docs/development-plan/` | Milestones and plans |
| `docs/guides/` | Feature guides and setup references |

When you make a significant architecture decision, add an ADR (next number, update the index in `docs/adr/README.md`). When a milestone starts or completes, update `docs/development-plan/README.md`.

## CI

GitHub Actions in `.github/workflows/`: `tests.yml` (build + test), `codeql.yml`, `docker-publish.yml` (image build/push on `main`, `dev`, `v*` tags). Dependabot groups NuGet updates weekly.
