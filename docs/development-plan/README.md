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

## Adding a milestone

1. Create a GitHub Milestone and attach issues/PRs to it.
2. Add a section here using the next `M<n>` number. Link the ADRs the milestone produces.
3. For larger milestones, add `M<n>_<NAME>_PLAN.md` in this folder with goals, tasks and "done when" criteria.
