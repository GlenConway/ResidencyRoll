# docs/adr — Architecture Decision Records

ADRs capture significant design choices, the context that drove them, and their consequences for ResidencyRoll.

ADR-001 to ADR-014 are **retrospective**. They were written on 2026-09-19 from the code and git history. Dates in each ADR give when the decision landed. ADRs from ADR-015 onward are written when the decision is made.

## Format

- **Status**: `Proposed`, `Accepted`, `Deprecated`, or `Superseded by ADR-NNN`
- **Context**: why the decision was needed
- **Decision**: what was decided
- **Consequences**: what changes as a result

## Index

| ADR | Title | Status |
|---|---|---|
| [ADR-001](ADR-001-split-api-and-web.md) | Split the Monolith into an API Backend and a Blazor Web Frontend | Accepted |
| [ADR-002](ADR-002-auth0-oidc-and-jwt-bearer.md) | Auth0 OpenID Connect for the Web App, JWT Bearer for the API | Accepted |
| [ADR-003](ADR-003-jwe-token-decryption.md) | Decrypt Auth0 JWE Access Tokens with the Client Secret | Accepted |
| [ADR-004](ADR-004-sqlite-with-ef-core.md) | SQLite with EF Core, Persisted on a Docker Volume | Accepted |
| [ADR-005](ADR-005-location-at-midnight-residency-model.md) | Location-at-Midnight Daily Presence Model | Accepted |
| [ADR-006](ADR-006-per-country-residency-rules.md) | Per-Country Residency Rules Table | Accepted |
| [ADR-007](ADR-007-gap-filling-between-trips.md) | Fill Daily Presence Gaps from the Last Arrival | Accepted |
| [ADR-008](ADR-008-timezone-aware-trips-with-iata-airports.md) | Timezone-Aware Trips Resolved from IATA Airport Codes | Accepted |
| [ADR-009](ADR-009-forwarded-headers-behind-reverse-proxy.md) | Configurable Forwarded Headers for Reverse-Proxy Deployments | Accepted |
| [ADR-010](ADR-010-itinerary-parsing-with-semantic-kernel.md) | Parse Pasted Flight Itineraries with Semantic Kernel and OpenAI | Accepted |
| [ADR-011](ADR-011-docker-and-github-actions-delivery.md) | Docker Images Built and Published by GitHub Actions | Accepted |
| [ADR-012](ADR-012-blazor-server-radzen-code-behind.md) | Blazor Server with Radzen Components and Code-Behind Files | Accepted |
| [ADR-013](ADR-013-carbon-design-system-replaces-liquid-glass.md) | Carbon Design System Replaces the Liquid Glass Theme | Accepted |
| [ADR-014](ADR-014-home-country-in-browser-storage.md) | Keep the Home Country in Browser localStorage | Accepted |

## Adding a new ADR

1. Copy an existing ADR as a template.
2. Name it `ADR-NNN-short-title.md` (increment the number).
3. Set Status to `Accepted` (or `Proposed` while under review).
4. Add a row to the index above.
5. If the new ADR supersedes an old one, set the old ADR's Status to `Superseded by ADR-NNN`.
