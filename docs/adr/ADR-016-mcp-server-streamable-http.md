# ADR-016: Expose ResidencyRoll to AI Clients as a Remote MCP Server over Streamable HTTP

## Status

Accepted (2026-11-09). Amended 2026-11-09: MCP structured output uses local wall-clock strings instead of RFC 3339 date-times; `get_days_per_country` includes the country rule; `forecast_trips` and `max_trip_end_date` report the peak inside the window; `get_location_on` projects past the last saved day; `get_current_location` added. Amended 2026-11-09 again: `locationAtMidnight` is 00:00 at the start of the date; threshold status `reached` means the count equals the threshold; planning and rolling-count tools were added. Country windows are in [ADR-018](ADR-018-per-country-counting-windows.md). Trip warnings, record coverage, write impact, and the Nova Scotia MSI minimum are in [ADR-019](ADR-019-trip-warnings-coverage-and-msi.md). Amended 2026-11-10 by [ADR-020](ADR-020-travellers-on-an-account.md): trip, residency, forecast, presence, and home-country tools take an optional `traveller`, and the home country is stored on the traveller. Amends [ADR-002](ADR-002-auth0-oidc-and-jwt-bearer.md) (the API accepts a second token audience for MCP clients) and [ADR-015](ADR-015-react-spa-replaces-blazor.md) (the Web host proxies `/mcp` and OAuth metadata as well as `/api`).

## Context

ResidencyRoll's data and residency engine are reachable only through the React UI and the REST API. The owner wants to ask AI clients questions such as "how many days have I spent in the UK in the last 365 days?" or "if I go to Portugal from 3 to 20 March, do I cross 183 days anywhere?", and to add trips by pasting an itinerary into a chat. Target clients are Claude Code first, then Claude Desktop, claude.ai custom connectors, Cursor, VS Code and the MCP Inspector.

The Model Context Protocol (MCP) is the standard these clients share. Remote servers use the **Streamable HTTP** transport: one endpoint takes JSON-RPC `POST`s and answers with either `application/json` or a `text/event-stream` (SSE) stream. For authorization, MCP uses **OAuth 2.1**. The server acts as an OAuth *protected resource*. It publishes Protected Resource Metadata (RFC 9728) that points clients at the authorization server (Auth0). Clients register themselves (Dynamic Client Registration, RFC 7591) or use a pre-registered client ID, sign the user in with PKCE, and send `Authorization: Bearer <token>` on every request. The token must be issued *for this MCP server* (audience / RFC 8707 `resource`). The server must reject tokens meant for anything else and must never pass a client's token on to another service.

Constraints from the existing system:

- The API already validates Auth0 JWT bearer tokens and scopes every query by the `sub` claim (`TripsController.GetUserId`). With JWT disabled it runs everyone as the local user (`LocalUserAuthenticationHandler`).
- Users reach the app through the **Web** container. Only that container is meant to face the internet, usually behind nginx, Caddy or a cloud load balancer.
- A good share of the residency logic sits in `TripsController`: forecast assembly, daily presence mapping, residency summary, max end date and standard durations. The airport table (IATA → city/country/IANA zone) lives only in the React app (`ClientApp/src/lib/airports.ts`).
- The home country is kept in browser localStorage (ADR-014), so the server does not know it.
- Deployment rule carried over from ADR-015: updating the images must keep working with the existing `docker-compose.yml` and `.env`. Any new setting must be optional and have a working default.

## Decision

### 1. Host the MCP server inside the API, at `/mcp`

The API project gets the official C# MCP SDK (`ModelContextProtocol.AspNetCore`; pin the latest stable release when implementing):

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<TripReadTools>()
    .WithTools<ResidencyTools>()
    .WithTools<TripWriteTools>();   // M10.6

app.MapMcp("/mcp").RequireAuthorization();
```

Reasons for putting it in the API:

- Tools call `TripService`, `ResidencyCalculationService` and `ItineraryParsingService` in-process, inside the same EF Core scope, with the same `sub`-based user scoping as REST.
- The API is already the JWT resource server, so MCP auth adds a second audience and nothing else.
- The Web BFF stays a cookie-only browser host. No token exchange code is needed.

**Stateless mode.** Each `POST` is handled on its own, with no `Mcp-Session-Id` held in memory. Container restarts, multiple replicas and proxies without sticky sessions all keep working. This rules out server-to-client requests (sampling, elicitation, roots) and server-pushed `listChanged` notifications. ResidencyRoll needs none of them. Responses may still stream over SSE, for example progress notifications during itinerary parsing.

### 2. Expose it publicly through the Web host

The Web host's YARP config gains two pass-through routes to the API cluster:

| Route | Destination | Transform |
|---|---|---|
| `/mcp` and `/mcp/{**rest}` | `{Api:BaseUrl}/mcp…` | **None.** The client's `Authorization` header is forwarded unchanged and the cookie token is never attached. |
| `/.well-known/oauth-protected-resource/{**rest}` and `/.well-known/oauth-protected-resource` | the same path on the API | none |

The BFF middleware (`MapBff`) must skip these paths: no CSRF check, no cookie-based `401`, no OIDC challenge. The `AttachAccessToken` transform must run only on the `api` route; move it from the global `AddTransforms` callback into a per-route transform. SSE responses must stream without buffering. YARP streams by default; set the cluster's `ActivityTimeout` to at least 5 minutes so long tool calls are not cut off.

Result: the MCP URL is `https://<the host users already browse to>/mcp`. Clients that can reach the API directly (for example the API on port 8080 inside a LAN) may use `http://<api-host>:8080/mcp` too. Both work, because the resource URL in the metadata is computed per request (§3).

### 3. Authorization

**Mode A: JWT disabled (`Jwt:Authority` unset).** `/mcp` runs as the local user with no auth and no protected-resource metadata. `claude mcp add --transport http residencyroll http://localhost:8081/mcp` works with nothing else to set. This matches how the UI behaves with OIDC off. Log a startup warning that `/mcp` is open, and keep the existing advice that this mode is for trusted networks only.

**Mode B: JWT enabled.** The API follows the MCP authorization spec:

1. An unauthenticated request to `/mcp` gets `401` with
   `WWW-Authenticate: Bearer resource_metadata="https://<host>/.well-known/oauth-protected-resource/mcp", scope="read:trips"`.
2. `GET /.well-known/oauth-protected-resource/mcp` (and the root form) returns:
   ```json
   {
     "resource": "https://<host>/mcp",
     "authorization_servers": ["https://<tenant>.auth0.com/"],
     "scopes_supported": ["read:trips", "write:trips"],
     "bearer_methods_supported": ["header"],
     "resource_name": "ResidencyRoll"
   }
   ```
   `<host>` comes from `Mcp:ResourceUrl` when set, otherwise from the request after forwarded headers (`UseConfiguredForwardedHeaders()` already runs first). Use the SDK's MCP authentication handler (`AddMcp(...)` / `McpAuthenticationDefaults`) if the pinned SDK version has it. Otherwise write a small endpoint and a `JwtBearerEvents.OnChallenge` override for `/mcp` paths only, so REST `401`s keep their current shape.
3. Token validation keeps the existing `AddJwtBearer` setup (issuer, lifetime, signature, optional JWE decryption from ADR-003). `ValidAudiences` becomes `{ Jwt:Audience, Mcp:Audience }`. `Mcp:Audience` defaults to the resource URL from step 2 when `Mcp:ResourceUrl` is set. A REST request with an MCP-audience token is fine, because both are the same user and the same API. An MCP request with a token for any other audience is rejected.
4. Scopes: tools are tagged read or write. A policy `mcp:read` requires scope `read:trips`, and `mcp:write` requires `write:trips`. The check runs inside each tool through a shared helper that reads the `scope` / `permissions` claim. A missing scope returns a tool error saying which scope is needed. It is not a transport-level `403`, because stateless MCP authorizes the whole endpoint and then individual calls. REST endpoints are left as they are; adding scope checks to REST is out of scope here.

**Auth0 setup (documented in `docs/guides/MCP_SETUP.md`):**

- Create an Auth0 **API** named "ResidencyRoll MCP" with identifier = the MCP resource URL (`https://<host>/mcp`), RS256, **token encryption off**, permissions `read:trips` and `write:trips`, and "Allow Offline Access" on so clients get refresh tokens.
- In **Settings → Advanced → Settings**, turn on **Resource Parameter Compatibility Profile**. With it on, Auth0 still checks `audience` first, but falls back to the RFC 8707 `resource` parameter that MCP clients send.
- Client registration. All of these are toggles in **Settings → Advanced → Settings**, and all were off in the `kinsac` tenant on 2026-11-09:
  - **Dynamic Client Registration (DCR)**: lets clients register themselves as third-party applications. Third-party applications can sign in only through connections that are marked as domain-level, so make the sign-in connection (for example Username-Password or Google) available to them. Claude Code, Claude Desktop, claude.ai and Cursor all handle DCR.
  - **Client ID Metadata Document (CIMD) Registration** and **Dynamic CIMD Registration** (Early Access): the client's ID is an HTTPS URL to a JSON document it hosts, and no Auth0 application is created per install. The 2025-11-25 MCP spec prefers this to DCR. Turn it on alongside DCR for clients that support it. Because it is Early Access, do not depend on it alone.
  - **Pre-registered client**: create a Native application "ResidencyRoll – AI clients" with the callback URLs each client documents (for example Claude Code's localhost callback). Users pass its client ID to the client (`claude mcp add … --client-id <id>`; check the current Claude Code flag names when writing the guide).
- Leave **Enable Application Connections** as it is. It only affects applications created later and is not needed when the connection is domain-level.
- On the MCP API, set **Application Access Policy → User-delegated Access** to *All apps allowed*. Under **Default Permissions for third-party applications**, set **User-delegated Access** to *All* and leave Client Access *Unauthorized*. Self-registered clients are third-party apps, and those get no permissions by default even when the policy allows them.

**Applied to the `kinsac` tenant on 2026-11-09:**
- DCR, Resource Parameter Compatibility Profile, CIMD Registration and Dynamic CIMD Registration are on.
- `google-oauth2` is promoted to domain level. `Username-Password-Authentication` is not, so self-registered clients can sign in with Google only.
- The API "ResidencyRoll MCP" exists:
  - identifier `https://residencyroll.kinsac.com/mcp`
  - RS256, JWE off
  - permissions `read:trips` and `write:trips`
  - offline access allowed
  - the access policies listed above
- The existing "ResidencyRoll API" (`https://residencyroll-api.kinsac.com`) is unchanged.
- `MCP_RESOURCE_URL` for production is `https://residencyroll.kinsac.com/mcp`.

### 4. Tool surface

Tools wrap shared services and are never copies of controller code. Before adding tools, move the logic currently in `TripsController` (forecast assembly, daily presence → DTO mapping, residency summary, max end date, standard durations) into an application service, for example `ResidencyQueryService`. The controller and the tools then both call it, and REST output stays byte-identical, which the existing tests protect.

| Tool | Scope | Annotations | Wraps |
|---|---|---|---|
| `list_trips(from?, to?)` | read | readOnly | `GET /trips` |
| `get_trip(id)` | read | readOnly | `GET /trips/{id}` |
| `get_residency_summary(startDate?, endDate?, homeCountry?)` | read | readOnly | `GET /trips/residency-summary`, plus days left/over and threshold status per country (the same rules the Home dashboard uses) |
| `get_days_per_country(window: "last365" \| "all")` | read | readOnly | `days-per-country`, `days-per-country/last365` |
| `get_daily_presence(startDate, endDate, country?, format?)` | read | readOnly | `GET /trips/daily-presence`, capped at 366 days per call. `format` `runs` returns contiguous stretches. `locationAtMidnight` is 00:00 at the start of the date |
| `get_rolling_count(country, from, to)` | read | readOnly | rolling 365-day total per day, plus whether the anniversary day counted |
| `get_location_on(date)` | read | readOnly | one day of daily presence: midnight location, countries visited, transit, and status (`recorded`, `gapFilled`, `projected`, `noData`) |
| `get_current_location()` | read | readOnly | current country, last arrival airport, and that arrival's local date |
| `get_country_rules(country?)` | read | readOnly | `ResidencyCalculationService.GetCountryRule` |
| `get_home_country(traveller?)` | read | readOnly | saved home country for the chosen traveller. Omit `traveller` for the signed-in person |
| `set_home_country(homeCountry, traveller?)` | write | idempotent | save or clear that traveller's home country |
| `forecast_trips(trips[], homeCountry?, country?, through?)` | read | readOnly | `POST /trips/forecast`: hypothetical legs → current vs forecast days, each on that country's window |
| `max_trip_end_date(..., trips?)` | read | readOnly | `POST /trips/forecast/max-end-date`. Optional unsaved legs |
| `max_trip_start_date(...)` | read | readOnly | earliest arrival before a fixed departure |
| `plan_max_presence(country, from, to, blackoutRanges?, dayLimit?)` | read | readOnly | dates that add the most days without passing the limit |
| `standard_duration_forecasts(..., trips?)` | read | readOnly | `POST /trips/forecast/standard-durations`. Optional unsaved legs |
| `lookup_airport(query)` | read | readOnly | server-side airport table (below) |
| `parse_itinerary(text)` | read | readOnly, openWorld | `ItineraryParsingService`; returns **draft** legs only and saves nothing. Listed only when the parser is configured. |
| `create_trip(leg)` / `create_trips(legs[])` | write | — | `POST /trips` |
| `update_trip(id, changes)` | write | idempotent | `PUT /trips/{id}` |
| `delete_trip(id)` | write | destructive | `DELETE /trips/{id}` |

Tool rules:

- Inputs use IATA codes plus local date-times (`yyyy-MM-ddTHH:mm`). The server resolves city, country and IANA timezone from a **server-side airport table**. Move the airport data from `ClientApp/src/lib/airports.ts` into a single JSON file in `ResidencyRoll.Shared`, loaded by the API and imported by the React build, so the two can never drift. Callers may also give `city`, `country` and `timezone` explicitly for airports not in the table. Validate IANA zones with `TimeZoneInfo.FindSystemTimeZoneById`.
- `homeCountry` is an optional argument wherever days left/over depend on it. When it is omitted, tools use the home country saved on the chosen traveller ([ADR-020](ADR-020-travellers-on-an-account.md)). `get_home_country` and `set_home_country` read and change that value. Omit `traveller` for the signed-in person.
- Outputs return `structuredContent` with an `outputSchema`, plus a short text summary for clients that only show text. Country names match the existing rules table. Local leg times are strings in `yyyy-MM-ddTHH:mm` with no offset, because they are wall-clock times in the airport's timezone. Date-only values use `yyyy-MM-dd`. The shared REST DTOs stay `DateTime`; MCP has its own output records.
- Tool descriptions state that each country uses either the midnight rule or the partial-day rule, and that `ruleType` in the result says which. `get_days_per_country` includes `ruleType` and `thresholdDays`. `forecast_trips` returns `windowStart`, `windowEnd`, per-country `peakDays` and `peakDate`, and threshold status, with both country lists sorted by days descending. `max_trip_end_date` returns that last midnight as a date, with the 365-day window (both ends counted) and `ruleType`. Its `daysAtLimit` is the peak rolling count in the window. `get_location_on` projects the last arrival country past the last saved day and marks that `projected`.
- Write tools validate exactly as REST does (same service methods). Clients ask the user to confirm because of the annotations. The server does not rely on that confirmation for safety.
- Not exposed: CSV import/export, database backup, system info. These are file and admin operations with no chat use.

Optional, low priority: an MCP **prompt** `residency_check` (asks for the home country, then calls the summary and forecast tools) and a **resource** `residencyroll://rules/countries`. Claude Code shows prompts as slash commands and resources as `@` mentions.

### 5. Transport hardening

- Validate the `Origin` header on `/mcp`. Reject any browser origin that is neither in `Cors:AllowedOrigins` nor in a new `Mcp:AllowedOrigins`. Allow requests with no `Origin`, as CLI and desktop clients send. This is the spec's DNS-rebinding defence.
- Allow CORS for `/mcp` and the metadata endpoint for configured origins only, exposing `WWW-Authenticate`, `Mcp-Session-Id` and `MCP-Protocol-Version`. This is needed by the browser-based MCP Inspector and claude.ai.
- Rate-limit `/mcp` per user (`sub`) with ASP.NET Core rate limiting, for example 120 requests a minute. Limit `parse_itinerary` more tightly because it spends OpenAI tokens.
- Do not log tokens. Downgrade the API's existing `OnMessageReceived` / `OnTokenValidated` claim dumps to `Debug`, since every MCP call would otherwise log the full claim set at `Information`.
- Cap request body size on `/mcp` (64 KB is plenty; itinerary text is the largest input).

### 6. Configuration

All new settings are optional:

| Setting | Env var (compose) | Default |
|---|---|---|
| `Mcp:Enabled` | `MCP_ENABLED` | `true` |
| `Mcp:ResourceUrl` | `MCP_RESOURCE_URL` | derived from the request |
| `Mcp:Audience` | `MCP_AUDIENCE` | `Mcp:ResourceUrl` when set, else none (only `Jwt:Audience` accepted) |
| `Mcp:AllowedOrigins:N` | `MCP_ALLOWED_ORIGIN_N` | none |

Add these lines to `docker-compose.yml` and `.env.example` with empty defaults. Compose passes empty strings, so treat empty as unset. Existing `.env` files keep working, and MCP is off for OAuth clients until the operator does the Auth0 setup in §3 and sets `MCP_RESOURCE_URL`. In Mode A it works straight away.

## Consequences

### Positive

- Claude Code and other MCP clients can query residency status, run what-if forecasts and add trips in conversation, under the user's own Auth0 identity and data.
- One code path for residency answers: REST, the React UI and MCP all use the same services.
- Stateless transport needs no session affinity and survives restarts.
- No new container, image or public port.

### Negative

- Operators who use Auth0 must do extra one-time Auth0 work (new API, resource-parameter profile, DCR or a native client) before OAuth MCP clients can connect.
- DCR creates one Auth0 application per client install. Auth0 tenants have application limits, and stale registrations need occasional clean-up.
- The API now accepts tokens with two audiences. Both map to the same user scoping, so this is acceptable, but it must be covered by tests.
- Moving logic out of `TripsController` is a refactor that carries some risk. The existing controller tests are the safety net and must pass unchanged.
- MCP is a fast-moving spec. Pin the SDK version and record the protocol revision in `docs/guides/MCP_SETUP.md`.

### Neutral

- Stateless mode means no server push. If a future feature needs elicitation or sampling, switch to stateful mode with sticky sessions or a distributed session store, which needs a new ADR.
- Clients with no OAuth support (some scripts and older tools) cannot connect in Mode B. M10.8 (personal access tokens) covers them if needed.

## Milestones (M10)

Work lands on `dev` through PRs, one milestone each. Every milestone leaves `/mcp` either disabled or fully working, so every published `dev` image is deployable. Each milestone ends with `dotnet test src/ResidencyRoll.sln` and `npm test` green.

### M10.1: Shared services and airport data (no MCP yet)

- Move forecast, daily presence mapping, residency summary, max end date and standard-duration logic from `TripsController` into `ResidencyQueryService`. The controller becomes thin.
- Move the airport table to `ResidencyRoll.Shared` as JSON, with an `AirportCatalog` service in the API (lookup by IATA code, search by text). The React app imports the same JSON, and `airports.test.ts` still passes.
- **Done when:** every existing API and Vitest test passes unchanged, and new unit tests cover `ResidencyQueryService` and `AirportCatalog`.

### M10.2: MCP endpoint with read tools (Mode A only)

- Add `ModelContextProtocol.AspNetCore`, register a stateless HTTP transport at `/mcp` behind `Mcp:Enabled`, and implement the read tools from §4 except forecasts and `parse_itinerary`.
- With JWT enabled, `/mcp` is **not mapped** in this milestone, so it cannot be reached without auth.
- Integration tests in `ResidencyRoll.Tests`: boot `ApiEntryPoint` with `WebApplicationFactory`, connect with the SDK's MCP client over the test server's `HttpClient`, then check `initialize`, `tools/list` and each tool against seeded trips. Assert that results equal the REST responses for the same data.
- **Done when:** `claude mcp add --transport http residencyroll http://localhost:5003/mcp` against a local API with JWT off lists the tools and answers "days per country in the last 365 days" correctly.

### M10.3: Forecast and itinerary tools

- Add `forecast_trips`, `max_trip_end_date`, `standard_duration_forecasts`, `parse_itinerary` (listed only when OpenAI is configured), `lookup_airport` and `get_country_rules`.
- Add `outputSchema` and `structuredContent` to every tool, and an MCP progress notification during `parse_itinerary`.
- **Done when:** integration tests cover each tool, including a Date Line forecast case and a parser-unavailable case.

### M10.4: Public route through the Web host

- Add the YARP routes from §2. Scope `AttachAccessToken` to the `api` route only, exclude `/mcp` and `/.well-known/oauth-protected-resource*` from the BFF middleware, and set the cluster `ActivityTimeout`.
- Add the `Origin` validation, CORS, body size limit and rate limits from §5.
- Web tests: `/mcp` forwards `Authorization` unchanged, never attaches the cookie's token, needs no CSRF header, and streams an SSE response. Extend the Playwright/compose smoke test with a JSON-RPC `initialize` `POST` to `http://localhost:8081/mcp`.
- Document reverse-proxy settings in `docs/guides/MCP_SETUP.md`: nginx `proxy_buffering off;` and a long `proxy_read_timeout` for `/mcp`; Caddy needs nothing.
- **Done when:** with `docker compose -f docker-compose.build.yml up`, Claude Code connects to `http://localhost:8081/mcp` (Mode A) and the read and forecast tools work.

### M10.5: OAuth for MCP (Mode B)

- Add the protected-resource metadata endpoints, the `WWW-Authenticate` challenge on `/mcp`, `ValidAudiences`, the `Mcp:*` settings with empty-means-unset handling, and per-tool scope checks.
- Map `/mcp` when JWT is enabled.
- Lower the JWT diagnostic logging to `Debug`.
- Tests sign their own JWTs with a test key (override `TokenValidationParameters` in the factory) and check:
  - no token → `401` with a correct `resource_metadata`
  - wrong audience → `401`
  - MCP audience → works
  - API audience → works on REST
  - `read:trips` only → write tools return a scope error
  - the metadata `resource` follows `Mcp:ResourceUrl` and `X-Forwarded-*`
- Write `docs/guides/MCP_SETUP.md` covering the Auth0 steps from §3, DCR and pre-registered client options, and how to add the server to Claude Code (`claude mcp add --transport http residencyroll https://<host>/mcp`, then `/mcp` → Authenticate), Claude Desktop/claude.ai (custom connector), Cursor, VS Code and the MCP Inspector.
- **Done when:** against the real Auth0 tenant, Claude Code completes the OAuth flow through the production-style Web URL and lists trips for the signed-in user only. A second Auth0 user sees only their own trips.

### M10.6: Write tools

- Add `create_trip`, `create_trips`, `update_trip` and `delete_trip` with the annotations and scope from §4, validated through the same service methods as REST. Airport resolution comes from `AirportCatalog`, with explicit city/country/timezone accepted as overrides.
- `create_trips` saves all or nothing in one transaction, so a bad leg in a parsed itinerary leaves no partial import.
- **Done when:** integration tests cover create/update/delete, cross-user isolation (user B cannot update or delete user A's trip; the call returns "not found"), invalid IATA and timezone, and transaction rollback. A manual check in Claude Code covers "paste itinerary → parse → confirm → create_trips".

### M10.7: In-app "Connect an AI client" panel and docs

- Add a section to the System Info page showing the MCP URL (`window.location.origin + "/mcp"`), copy buttons for the Claude Code command and a JSON snippet for clients configured by file, and whether OAuth is required. Add a `GET /bff/mcp-info` endpoint on the Web host so the SPA knows `Mcp:Enabled` and the mode.
- Update `README.md`, `CLAUDE.md` (solution layout: `Mcp/` folder in the API), `docs/development-plan/README.md` and `.github/copilot-instructions.md`.
- **Done when:** Vitest covers the panel, and the docs let a new operator connect Claude Code in under 10 minutes.

### M10.8 (optional): Personal access tokens

Only if clients without OAuth turn out to matter.

- Add a `PersonalAccessToken` table (EF migration generated with `dotnet ef`). Store a hash only, with a name, scopes, expiry and last-used time. The System Info page can create (shown once) and revoke tokens.
- The API accepts `Authorization: Bearer rr_pat_…` on `/mcp` only, through a second authentication scheme picked by token prefix.
- Needs its own ADR before work starts, because it creates a second credential type.

## Out of scope

- Stateful sessions, sampling, elicitation and server-pushed notifications.
- The legacy HTTP+SSE transport (protocol revision 2024-11-05) and stdio. Claude Code and current clients speak Streamable HTTP. A stdio wrapper can be added later with `npx mcp-remote https://<host>/mcp` and no server changes.
- Exposing CSV import/export, backups or system administration over MCP.
- Scope enforcement on the existing REST endpoints.
