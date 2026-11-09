# ADR-015: Replace the Blazor Server UI with a React SPA Served by the Existing Web Host

## Status

Accepted (2026-11-09). Supersedes [ADR-012](ADR-012-blazor-server-radzen-code-behind.md). Amends [ADR-001](ADR-001-split-api-and-web.md) (the Web frontend is React instead of Blazor) and [ADR-013](ADR-013-carbon-design-system-replaces-liquid-glass.md) (Carbon comes from `@carbon/react` instead of hand-written CSS over Radzen).

## Context

The Web app is Blazor Server with Radzen components (ADR-012). On 2026-11-09 the app started failing intermittently after sign-in and on ordinary page loads with "An unhandled error has occurred":

- Client console: `There was an error applying batch 2.`
- Server log: `TypeError: Cannot read properties of null (reading 'insertBefore')` and `No element is currently associated with component 1`, followed by circuit termination.

It reproduced locally on every page (Home, System Info, `/not-found`), roughly half of all loads, with no third-party script touching the DOM. Updating to ASP.NET Core 10.0.14 and Radzen 12.2.0 did not fix it. Removing the per-page `@rendermode`, the extra `AddServerSideBlazor()` call, the `<CascadingAuthenticationState>` wrapper, and moving JS interop to `OnAfterRenderAsync` did not fix it either. The fault sits inside Blazor's circuit/DOM synchronisation, where the project has little visibility or control.

Other costs of Blazor Server for this app:

- Every user holds a server circuit over a WebSocket. Reverse proxies, idle timeouts and container restarts drop the UI into "Rejoining the server...".
- Debugging means reading minified `blazor.web.js` with no stack traces.
- Radzen theming needed many CSS overrides (ADR-013).

The owner has lost confidence in Blazor and wants a React UI, deployed by pulling a new `web` image with nothing else to change.

## Decision

### 1. Keep the `ResidencyRoll.Web` host as a Backend-for-Frontend (BFF) and serve a React SPA from it

The ASP.NET Core project `src/ResidencyRoll.Web` stays and keeps its name, its Docker image, its port and its configuration. It stops rendering UI and instead:

1. Serves the compiled React app (static files in `wwwroot`) with an `index.html` fallback for client-side routes.
2. Keeps the existing OpenID Connect + cookie sign-in exactly as configured today (Auth0, `/signin-oidc`, `SaveTokens = true`, audience parameter, `MapInboundClaims = false`, name claim `name`).
3. Reverse-proxies `/api/**` to the API container (`Api:BaseUrl`) and attaches the signed-in user's access token as a bearer token. The browser never sees a token.

This is the BFF pattern. It was chosen over a token-in-the-browser SPA (Auth0 SPA SDK + PKCE) because it needs **no Auth0 application changes, no new environment variables and no API changes**, and it keeps tokens out of JavaScript.

### 2. Frontend stack

| Concern | Choice |
|---|---|
| Build | Vite + React 19 + TypeScript (strict) |
| Location | `src/ResidencyRoll.Web/ClientApp/` |
| UI components | `@carbon/react` + `@carbon/styles` (keeps the Carbon look from ADR-013, with dark mode via Carbon themes `g10`/`g100`) |
| Routing | React Router (routes `/`, `/manage-trips`, `/forecast`, `/system-info`, `*` = not found) |
| Server state | TanStack Query |
| Dates/timezones | Luxon (IANA timezone maths: offsets, local → UTC) |
| API types | Generated from the API's Swagger document with `openapi-typescript`, committed to `ClientApp/src/api/schema.ts` |
| Unit/component tests | Vitest + React Testing Library |
| End-to-end tests | Playwright (smoke + parity flows) |
| Lint/format | ESLint (typescript-eslint, react-hooks) + Prettier |
| Package manager | npm with a committed `package-lock.json`; pin exact versions |

Package versions are pinned exactly in `package.json`. Node runs at the current LTS (22.x) in CI and in the Docker build stage.

### 3. BFF endpoints (server contract the SPA depends on)

| Endpoint | Behaviour |
|---|---|
| `GET /auth/login?returnUrl=/path` | OIDC challenge, then redirect to `returnUrl` (local URLs only). With OIDC disabled, redirect to `returnUrl`. |
| `POST /auth/logout` | Sign out of cookie + OIDC (Auth0 logout), return to `/`. Requires the CSRF header (below). Keep `POST /logout` as an alias for one release. |
| `GET /bff/user` | `200 { name, email, isAuthenticated: true }` or `401`. With OIDC disabled, return a fixed local user. |
| `/api/{**path}` | Reverse proxy to `{Api:BaseUrl}/api/{path}` with `Authorization: Bearer <access_token>`. Streams request and response bodies (CSV export, CSV import, backup download). Returns `401` (never a redirect) when there is no session. Use YARP (`Yarp.ReverseProxy`) with a request transform that reads `HttpContext.GetTokenAsync("access_token")`. |
| `/{**}` (non-file) | Serves `index.html`. Unauthenticated HTML navigations get the OIDC challenge, as `GET /` does today. |

CSRF: every non-GET request to `/api/**` and `/auth/logout` must carry `X-CSRF: 1`; the BFF rejects requests without it with `400`. The auth cookie stays `SameSite=Lax`. This replaces Blazor's antiforgery.

Data Protection keys persist to `/app/data/keys` (the `residencyroll-web-data` volume that compose already mounts), so sessions survive container restarts. Today they do not.

### 4. Deployment contract ("update the image and it just works")

The cut-over release must hold every one of these:

- Image names, tags and the publish workflow stay the same (`.../web`, `.../api`).
- The web container still listens on `8080` and runs `dotnet ResidencyRoll.Web.dll`.
- `docker-compose.yml`, `docker-compose.build.yml` and `.env.example` need **no edits**. Every existing environment variable keeps its meaning. Any new setting is optional with a working default.
- Auth0 settings stay as they are: callback `https://<host>/signin-oidc`, logout return URL `https://<host>/`, same client ID/secret, same audience.
- The API image and its endpoints do not change. (API changes are allowed later, but the migration must not depend on them.)
- Forwarded headers (`UseConfiguredForwardedHeaders()`) still run first in the pipeline.
- The home country keeps the localStorage key `residencyroll_home_country`, so each browser's saved choice carries over.
- Users sign in once after the upgrade (the cookie format may change). Nothing else is visible.

### 5. Migration approach

Work lands on `dev` milestone by milestone through PRs. **Blazor keeps serving the UI until the cut-over milestone (M9.7)**, so every `dev` image published along the way still works. Before cut-over, the React app runs locally on the Vite dev server, which proxies `/api`, `/auth` and `/bff` to the Web host.

## UI parity checklist

Every row must work in React before cut-over. "Source" names the Blazor file whose behaviour is the spec. Read it before building the React version.

### Shell and auth

| # | Feature | Source |
|---|---|---|
| S1 | Header: title "Residency Roll - Residency Status Dashboard", signed-in user name, Logout button | `Components/Layout/MainLayout.razor`, `Components/LoginDisplay.razor` |
| S2 | Side navigation: Home, Manage Trips, Forecast, System Info, with active-route highlight | `Components/Layout/NavMenu.razor` |
| S3 | All four pages require sign-in; unauthenticated visits go straight to Auth0 | `[Authorize]` on pages, `Program.cs` auth setup |
| S4 | Logout ends the cookie session and the Auth0 session, returns to `/` | `Program.cs` `MapPost("/logout")` |
| S5 | Not-found page for unknown routes; generic error page/boundary | `Components/Pages/NotFound.razor`, `Error.razor` |
| S6 | Page titles per route (`Residency Roll - Travel Tracker`, `Manage Trips`, `Forecast - Residency Roll`, `System Info - Residency Roll`) | `<PageTitle>` in each page |
| S7 | Carbon theme, light and dark mode, focus/contrast rules | `wwwroot/carbon-theme.css`, `carbon-utils.css`, `accessibility.css` |
| S8 | Expired session: an API `401` sends the browser to `/auth/login?returnUrl=<current path>` | new (Blazor showed a broken circuit) |

### Home (`/`)

| # | Feature | Source |
|---|---|---|
| H1 | Residency Status Dashboard: start/end date pickers (default today−365 → today), **Calculate** button, loading spinner, empty-state message | `Components/ResidencyStatusDashboard.razor(.cs)` |
| H2 | One card per country: name, rule badge (Midnight / PartialDay), big day count, "Midnights" vs "Partial Days" label, progress bar vs threshold, threshold text, rule explanation caption | same |
| H3 | Card status: Home Country badge; "⚠️ N days left" when approaching; "⛔ Over Threshold"; otherwise "✓ N days remaining". Border/progress colour red (over, not home), orange (approaching), green (safe or home) | `GetCardStyle`, `GetProgressBarStyle`, markup |
| H4 | **Export CSV** of the daily presence log for the chosen range (`GET /api/v1/trips/daily-presence-export?startDate&endDate`), downloaded with the server's filename | `ExportDailyPresence` |
| H5 | Daily Presence Timeline: start/end pickers that reload on change, home-country dropdown (shown when countries exist) | `Components/ResidencyTimeline.razor(.cs)` |
| H6 | Legend: one swatch per country (home shown neutral with "(home)"), plus Transit (`#FF9800`) | same |
| H7 | Grid grouped by month (newest first), one cell per day with day number and label: "Transit", stacked country codes for multi-country days, or a single country code | `GetGroupedByMonth`, `GetCountryCode`, `GetDayStyle`, `GetDayClass` |
| H8 | Multi-country day cells use a gradient of each country's colour; home country cells are neutral | `GetDayStyle` |
| H9 | Tooltip per day (`MMM dd, yyyy - …`, transit and partial-day wording) | `GetDayTooltip` |
| H10 | Click a day → details panel: transit message, multi-country partial-day explanation, or location-at-midnight text; "Countries visited during day" | timeline markup |
| H11 | Country colours: 20-colour palette assigned in sorted country order, stable within a page | `Services/CountryColorService.cs` |
| H12 | Home country: read from localStorage key `residencyroll_home_country`; if unset or not in the data, default to the first country alphabetically; saving the dropdown writes the key; the dashboard and forecast read the same key | `ResidencyTimeline.razor.cs`, ADR-014 |

### Manage Trips (`/manage-trips`)

| # | Feature | Source |
|---|---|---|
| T1 | Trip list sorted by departure (newest first), with column sorting and filtering | `Components/Pages/ManageTrips.razor(.cs)` |
| T2 | Columns: Departure (`IATA - City` + UTC offset), Departure Time (`yyyy-MM-dd HH:mm`), Arrival (same format), Arrival Time, Duration (`N hours`), actions | markup, `GetTimezoneOffset` |
| T3 | Row actions: Edit (opens editor), **Add Leg** (new trip pre-filled from this trip's arrival: departs 2h after arrival from the same city/country/timezone/IATA, 2h duration), Delete (add a confirmation dialog in React) | `EditRow`, `AddLeg`, `Helpers/TripLegFactory.cs`, `DeleteRow` |
| T4 | **Add Trip** buttons above and below the list open the editor in "Add New Trip" mode (defaults: today 12:00, UTC) | `InsertRow` |
| T5 | Editor view (Blazor shows it as a second tab): Departure and Arrival sections, each an Airport Selector with date and time pickers | editor markup |
| T6 | Changing the departure date on a **new** trip copies it to the arrival date; on an existing trip it leaves arrival alone | `Helpers/TripDateHelper.cs` |
| T7 | Validation: all date/time fields required; Departure/Arrival City, Country and Timezone required, listed by name in the error message | `SaveDetailedTrip` |
| T8 | **Save Trip**, **Save and Add Leg** (saves, then opens a next-leg editor from the saved trip), **Cancel** | `SaveDetailedTrip(addLegAfterSave)` |
| T9 | Import/Export panel toggle: **Export Trips (CSV)** (`GET /api/v1/trips/export`); file picker + **Import** (`POST /api/v1/trips/import`, multipart), result message green/red, list reloads | `ToggleImportExport`, `ExportTrips`, `OnImport` |
| T10 | Itinerary parser (new trips only): shows when `GET /api/v1/trips/parse-itinerary/availability` is true, otherwise an info alert | `CheckItineraryParserAvailability` |
| T11 | Parser modal: textarea with example placeholder, **Parse Itinerary** / **Clear** / **Close**, "Analyzing your itinerary with AI..." spinner, error alert | `Components/Dialogs/ItineraryParserDialog.razor(.cs)` |
| T12 | Parsed legs become draft trips (airport lookup by IATA fills city/country/timezone; unknown codes fall back to the code and `UTC`; `yyyy-MM-ddTHH:mm` local times; missing arrival = departure). Editor steps through drafts with **Previous Leg** / **Next Leg**, shows "Parsed legs: N (Leg i selected)", **Save Parsed Legs** validates every draft and creates them all | `ApplyParsedTripsAsync`, `SaveParsedDraftTrips`, `ShowPrevious/NextParsedDraft` |

### Airport Selector (shared by Manage Trips and Forecast)

| # | Feature | Source |
|---|---|---|
| A1 | Autocomplete over the bundled airport list (109 airports, IATA code + name + city + country + IANA zone), case-insensitive across code/city/name/country, 300 ms debounce, first 20 shown before typing, max 50 results | `Components/AirportSelector.razor(.cs)`, `Data/AirportData.cs` |
| A2 | Selecting an airport sets city, country, IANA timezone and IATA code; caption shows "City, Country" | `OnAirportSelected` |
| A3 | Initial value comes from the IATA code, else from a matching city | `UpdateSearchTextFromParameters` |
| A4 | UTC offset badge (`UTC+HH:MM`, current offset of the zone) | `GetTimezoneOffset` |
| A5 | "Manual timezone" checkbox → searchable timezone dropdown (the curated list in `LoadTimezones`); picking one sets city/country from that entry and clears the IATA code | `OnManualTimezoneChanged`, `LoadTimezones` |

### Forecast (`/forecast`)

| # | Feature | Source |
|---|---|---|
| F1 | Itinerary parser card when available (same modal as T11), else info alert. Parsed legs **replace** the leg list; success alert "Parsed N flight leg(s)" | `Components/Pages/Forecast.razor(.cs)`, `ApplyParsedLegs` |
| F2 | Multi-leg planner: starts with one leg (departure and arrival one month from today, 12:00); **Add Leg**; delete button per leg when more than one; leg labels Outbound / Connecting / Return; "Total legs: N" | `AddLeg`, `RemoveLeg`, markup |
| F3 | Each leg: departure and arrival Airport Selectors with date + time; changing a leg's departure date sets its arrival date to match | `OnDepartureDateChanged` |
| F4 | **Calculate Forecast** validates (arrival country, all dates/times) and lists "Leg N: missing …" warnings | `CalculateForecast`, `BuildValidationIssues` |
| F5 | Calls `POST /api/v1/trips/forecast` with the legs; shows two columns: **Current (Last 365 Days)** and **Forecast (365 Days from Trip End)**, each sorted by days, "(home)" marker, "Total Away (excl. home)" row that excludes the home country | results markup |
| F6 | Forecast rows for visited countries: green border, or red border with "⚠️ Exceeds 183" when over 183 and not home; "(+ N days)" trip duration computed in UTC from first arrival to last departure | `GetTripDurationDays`, `ConvertToUtc` |
| F7 | 183-Day Limit Warning panel for the primary destination (country with the longest stay between arrival and next departure) when it exceeds 183 and is not home | `GetPrimaryDestinationCountry` |
| F8 | Error messages when the API is unreachable or fails | `CalculateForecast` catch blocks |

### System Info (`/system-info`)

| # | Feature | Source |
|---|---|---|
| Y1 | Database card: location, file found/not found, size (human-readable), users, trips | `Components/Pages/SystemInfo.razor(.cs)` |
| Y2 | Backups card: next scheduled backup (UTC, weekly Mondays) or "Scheduled backups are disabled."; **Backup now** with busy state and success/failure alert; list reloads | `BackupNowAsync` |
| Y3 | Backup table: file, created (UTC), size, **Download** (`GET /api/v1/system/backups/{fileName}` through the proxy, browser download) | markup |
| Y4 | Application card: API version, environment; loading and error states | markup |

### Deliberately not migrated

- `Components/TripTimeline.razor` and `Components/TimezoneSelector.razor(.cs)`: not used by any page.
- `/app/trips/*` and `/app/system/backups/*` proxy endpoints in `Program.cs`: replaced by the generic `/api/**` proxy.
- `Components/Pages/Login.razor`: the BFF challenges directly.
- `ReconnectModal`: Blazor-only.
- Bootstrap (`wwwroot/lib/bootstrap`) and Bootstrap Icons: replaced by Carbon components and `@carbon/icons-react`.

## Milestones

Each milestone is one PR into `dev` and must pass `dotnet test`, `npm run lint`, `npm run typecheck`, `npm test` and `npm run build`. Tick the parity rows as they land.

### M9.1 — BFF endpoints in the Web host (Blazor still serves the UI)

- Add YARP and the `/api/{**path}` proxy with the bearer-token transform, `401` for unauthenticated API calls (the default challenge scheme is OIDC, so in `OpenIdConnectEvents.OnRedirectToIdentityProvider` return `401` and call `HandleResponse()` when the path starts with `/api` or `/bff`; do the same in the cookie scheme's `OnRedirectToLogin` for the OIDC-disabled mode), and the `X-CSRF` check on non-GET requests.
- Add `GET /auth/login?returnUrl=`, `POST /auth/logout`, `GET /bff/user`. Keep `POST /auth/login` and `POST /logout` working for Blazor.
- Persist Data Protection keys to `/app/data/keys` (path configurable via `DataProtection:KeysPath`, default `/app/data/keys` in containers and `./data/keys` locally).
- OIDC disabled (`Authentication:OpenIdConnect:Enabled=false`) keeps working: proxy without a token, `/bff/user` returns a local user.
- Tests (xUnit + `Microsoft.AspNetCore.TestHost`): proxy forwards path/query/body and attaches the token; anonymous API call gets `401`; POST without `X-CSRF` gets `400`; `returnUrl` rejects absolute URLs.

**Done when** the existing Blazor UI still works and `curl` against `/api/v1/trips` with a session cookie returns the user's trips.

### M9.2 — React scaffold, shell and shared building blocks

- Create `src/ResidencyRoll.Web/ClientApp` (Vite + React + TS strict, Carbon, React Router, TanStack Query, Luxon, ESLint, Prettier, Vitest, Playwright).
- Vite dev server proxies `/api`, `/auth`, `/bff`, `/signin-oidc`, `/signout-callback-oidc` to `https://localhost:5113`.
- Generate `src/api/schema.ts` from the API Swagger (`npm run gen:api`, documented in the README) and write a typed `apiClient` that adds `X-CSRF: 1`, redirects to `/auth/login?returnUrl=` on `401`, and downloads files from the `Content-Disposition` filename.
- Build the shell: header, side nav, route table, page titles, not-found, error boundary, light/dark theme (S1–S8).
- Port shared logic to TypeScript with unit tests that copy the C# test cases: airport data and search (`Data/AirportData.cs`), timezone list and offset formatting, `TripDateHelper`, `TripLegFactory`, country colours, country codes, `useHomeCountry` hook (localStorage key `residencyroll_home_country`). Port the cases in `src/ResidencyRoll.Tests/TripDateHelperTests.cs`, `TripLegFactoryTests.cs`, `TripDurationTests.cs` and `TripSaveAndAddLegTests.cs`.
- Build the `AirportSelector` component (A1–A5) and the `ItineraryParserModal` (T11).
- Add a Node build stage to `Dockerfile` that runs `npm ci && npm run build` and copies the output into the publish folder's `wwwroot/app-next/` (built and shipped but not served yet, so the Docker build is proven early).
- CI: add a `frontend` job to `.github/workflows/tests.yml` (setup-node with npm cache, `npm ci`, lint, typecheck, test, build). Add the npm ecosystem for `src/ResidencyRoll.Web/ClientApp` to Dependabot.

**Done when** `npm run dev` shows the shell signed in against the local Web host and the ported unit tests pass.

### M9.3 — Home page

- Residency Status Dashboard (H1–H4) using `GET /api/v1/trips/residency-summary` and the CSV export.
- Daily Presence Timeline (H5–H12) using `GET /api/v1/trips/daily-presence`.
- Component tests for card status colours/badges, month grouping, multi-country cells, home-country defaulting.

**Done when** Home matches the Blazor page side by side for the same account and date range.

### M9.4 — Manage Trips

- Trip list (T1–T4) with a Carbon `DataTable` (sorting, a text filter, row actions, delete confirmation).
- Trip editor (T5–T8) as a routed view or Carbon `Tabs` panel, using `AirportSelector`.
- Import/export (T9) and itinerary parser with draft legs (T10, T12).
- Component tests for validation messages, save-and-add-leg pre-fill and draft navigation; one Playwright flow: add trip → add leg → edit → delete.

**Done when** every T and A row works against the local stack.

### M9.5 — Forecast

- Leg planner, parser, validation and results (F1–F8).
- Unit tests for trip duration (UTC, cross-timezone), primary destination, and "Total Away (excl. home)".

**Done when** the forecast for a sample multi-leg trip matches the Blazor results for the same data.

### M9.6 — System Info

- Y1–Y4, including backup download through the proxy.

**Done when** Backup now creates a file and Download saves it.

### M9.7 — Cut-over: serve React, remove Blazor

- `Program.cs`: remove `AddRazorComponents`, `MapRazorComponents`, `AddServerSideBlazor`, `AddRadzenComponents`, `AddCascadingAuthenticationState`, the `/app/trips` and `/app/system` proxies, `TripsApiClient`, `SystemApiClient`, `AccessTokenProvider`, `ApiAuthenticationHandler` (if the YARP transform replaced it), `LocalStorageService`, `CountryColorService`. Serve static files and `MapFallbackToFile("index.html")` with the auth challenge for HTML navigations (section 3).
- Delete `Components/`, `Helpers/`, `Data/`, Blazor CSS and `wwwroot/lib/bootstrap`. Remove `Radzen.Blazor` from the csproj. The Vite build writes straight into `wwwroot` (git-ignored).
- Move or delete C# tests that referenced `ResidencyRoll.Web` helpers (their cases now live in Vitest) and drop the Web project reference from `ResidencyRoll.Tests.csproj` if nothing else needs it.
- `Dockerfile`: Node stage builds the SPA into `wwwroot` before `dotnet publish`; final stage unchanged (`aspnet:10.0`, port 8080, same entrypoint).
- Playwright smoke suite (runs in CI against `docker compose -f docker-compose.build.yml` with `OIDC_ENABLED=false`): every route renders, a trip can be created and deleted, forecast calculates, CSV downloads.
- Verify the deployment contract (section 4) item by item, including `docker compose up -d` with an **unchanged** `.env`.
- Docs: update `CLAUDE.md` (solution layout, commands, conventions: replace the Blazor code-behind rule with React/TS conventions), replace `.github/copilot-instructions.md` with React guidance, update `README.md`, set ADR-012 to Superseded (done), and mark M9 complete in `docs/development-plan/README.md`.

**Done when** pulling the new `web` image and running `docker compose up -d` with the existing `.env` gives a working React UI with every parity row ticked.

## Consequences

- No server circuits or WebSockets for the UI. The Web host becomes a stateless BFF and static file server.
- The browser never holds access tokens; Auth0 and API configuration stay as they are.
- The repo gains a Node toolchain: Node in CI and in the Docker build stage, npm Dependabot updates, a lockfile to maintain.
- Shared C# DTOs no longer reach the UI directly. Types come from the generated OpenAPI schema, so API contract changes surface as TypeScript errors after `npm run gen:api`.
- UI logic that lived in C# (airport data, timezone offsets, trip duration, leg factory) is duplicated in TypeScript for the browser. The API remains the source of truth for residency calculations.
- Sessions survive container restarts once Data Protection keys persist to the volume.
- The home country still lives in localStorage (ADR-014 unchanged).
