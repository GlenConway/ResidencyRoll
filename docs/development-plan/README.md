# Development Plan

Milestones for ResidencyRoll. M1 to M8 are **retrospective**: they were reconstructed on 2026-09-19 from git history and group work that already shipped. Dates are commit dates. Track new milestones as GitHub Milestones and link the issues and PRs here.

## Milestones

### ✅ M1 — Travel Tracker MVP (Complete, 2026-01-01 to 2026-01-02)

Single Blazor Server app with trip CRUD, rolling 365-day dashboard (gauge, distribution chart, timeline), trip management grid, CSV import/export, and a forecast page with a 183-day planner. SQLite storage.

**ADR:** [ADR-004](../adr/ADR-004-sqlite-with-ef-core.md), [ADR-012](../adr/ADR-012-blazor-server-radzen-code-behind.md)

---

### ✅ M2 — API/Web Split and Authentication (Complete, 2026-01-11 to 2026-01-17)

Separated API, Web and Shared projects. Added OpenID Connect sign-in, JWT bearer validation, JWE token support, per-user trips, and reverse-proxy forwarded headers.

**ADRs:** [ADR-001](../adr/ADR-001-split-api-and-web.md) · [ADR-002](../adr/ADR-002-auth0-oidc-and-jwt-bearer.md) · [ADR-003](../adr/ADR-003-jwe-token-decryption.md) · [ADR-009](../adr/ADR-009-forwarded-headers-behind-reverse-proxy.md)

---

### ✅ M3 — Delivery Pipeline (Complete, 2026-01-01 to 2026-01-19)

Docker images for API and Web, GitHub Actions for build/test, CodeQL and image publishing, path filters, version stamping, and grouped Dependabot updates.

**ADR:** [ADR-011](../adr/ADR-011-docker-and-github-actions-delivery.md)

---

### ✅ M4 — Timezone-Aware Trips (Complete, 2026-01-17 to 2026-02-03)

Trips became flight legs with IATA airports and IANA timezones. Added airport autocomplete, timezone display, save-and-add-leg, overlapping-trip handling, migration reset and removal of legacy columns.

**ADR:** [ADR-008](../adr/ADR-008-timezone-aware-trips-with-iata-airports.md)

---

### ✅ M5 — Location-at-Midnight Residency Engine (Complete, 2026-01-17 to 2026-01-19)

Replaced duration-based counting with daily presence, per-country rules, gap filling, International Date Line handling, and a forecast built on the same engine with multi-leg support.

**ADRs:** [ADR-005](../adr/ADR-005-location-at-midnight-residency-model.md) · [ADR-006](../adr/ADR-006-per-country-residency-rules.md) · [ADR-007](../adr/ADR-007-gap-filling-between-trips.md)

---

### ✅ M6 — UI Design System (Complete, 2026-01-23 to 2026-02-19)

Liquid Glass theme with accessibility work, then a migration to a Carbon-based theme with opaque surfaces and dark mode. Country colours and home-country designation on the timeline.

**ADRs:** [ADR-013](../adr/ADR-013-carbon-design-system-replaces-liquid-glass.md) · [ADR-014](../adr/ADR-014-home-country-in-browser-storage.md)

---

### ✅ M7 — Itinerary Parsing (Complete, 2026-02-06 to 2026-02-28)

Paste a booking itinerary or flight text on the Forecast and Manage Trips pages and get structured legs, using Semantic Kernel with OpenAI. Extracts departure and arrival times.

**ADR:** [ADR-010](../adr/ADR-010-itinerary-parsing-with-semantic-kernel.md)

---

### ✅ M8 — Residency Dashboard Enhancements (Complete, 2026-03 to 2026-04)

Days left/over relative to home country, date range picker on the residency status dashboard, and CSV export of the daily presence log.

**ADR:** [ADR-006](../adr/ADR-006-per-country-residency-rules.md)

---

### ✅ M9 — Migrate the Web UI from Blazor to React (Complete, 2026-11-09)

Replace the Blazor Server UI with a React SPA served by the existing `ResidencyRoll.Web` host acting as a BFF. Sub-milestones, the UI parity checklist and the deployment contract (same images, ports, env vars and Auth0 settings) are in the ADR.

- ✅ M9.1 — BFF endpoints in the Web host (Blazor still serves the UI)
- ✅ M9.2 — React scaffold, shell and shared building blocks
- ✅ M9.3 — Home page
- ✅ M9.4 — Manage Trips
- ✅ M9.5 — Forecast
- ✅ M9.6 — System Info
- ✅ M9.7 — Cut-over: serve React, remove Blazor

**ADR:** [ADR-015](../adr/ADR-015-react-spa-replaces-blazor.md)

---

### ✅ M10 — MCP Server for AI Clients (Complete, 2026-11-09)

Expose residency queries, forecasts and trip editing to Claude Code and other MCP clients through a stateless Streamable HTTP endpoint at `/mcp`, hosted in the API and published through the Web host, secured with OAuth 2.1 via Auth0. Design, tool list and "done when" criteria are in the ADR. Setup steps are in [Connect an AI client](../guides/MCP_SETUP.md).

- ✅ M10.1 — Shared residency query service and server-side airport data
- ✅ M10.2 — MCP endpoint with read tools (auth disabled mode)
- ✅ M10.3 — Forecast and itinerary tools
- ✅ M10.4 — Public `/mcp` route through the Web host, transport hardening
- ✅ M10.5 — OAuth for MCP (protected resource metadata, audiences, scopes, Auth0 guide)
- ✅ M10.6 — Write tools
- ✅ M10.7 — "Connect an AI client" panel and docs
- ✅ Follow-up — MCP local-time output schemas, country rules on `get_days_per_country`, forecast peaks, location projection, `get_current_location`, and a saved home country ([ADR-017](../adr/ADR-017-home-country-on-the-user-profile.md))
- ✅ Follow-up — Trip warnings, record coverage, write impact, partial `update_trip`, and the Nova Scotia MSI minimum ([ADR-019](../adr/ADR-019-trip-warnings-coverage-and-msi.md))
- M10.8 — (Optional, not started) Personal access tokens. Needs its own ADR before any work.

**ADR:** [ADR-016](../adr/ADR-016-mcp-server-streamable-http.md)

---

### ✅ M11 — Travellers on One Account (Complete, 2026-11-10)

Track trips and residency for several people, such as a spouse who does not sign in, from one login. Each traveller has their own trips and home country. The UI and MCP default to the account owner, and MCP tools take an optional `traveller` on every call. Plan and "done when" criteria are in [M11_TRAVELLERS_PLAN.md](M11_TRAVELLERS_PLAN.md).

- ✅ M11.1 — Data model and migration
- ✅ M11.2 — Service layer scoped by traveller
- ✅ M11.3 — REST API
- ✅ M11.4 — React UI
- ✅ M11.5 — MCP tools and traveller parameter
- ✅ M11.6 — Docs

**ADR:** [ADR-020](../adr/ADR-020-travellers-on-an-account.md)

---

### ✅ M12 — MCP itinerary batch (Complete, 2026-11-10)

Batch validation, atomic delete and replace, compact write results, overlap handling, reconciliation, CSV over MCP, trip provenance, a full airport table, and `get_server_info`.

**ADR:** [ADR-021](../adr/ADR-021-mcp-itinerary-batch.md)

---

## Adding a milestone

1. Create a GitHub Milestone and attach issues/PRs to it.
2. Add a section here using the next `M<n>` number. Link the ADRs the milestone produces.
3. For larger milestones, add `M<n>_<NAME>_PLAN.md` in this folder with goals, tasks and "done when" criteria.
