# ADR-001: Split the Monolith into an API Backend and a Blazor Web Frontend

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-01-11 (`refactor: Split monolithic Blazor app into clean API backend and frontend architecture`).

## Context

ResidencyRoll began as a single Blazor Server app (2026-01-01) where pages called `TripService` and `ApplicationDbContext` directly. Business logic and persistence sat inside the UI host, which blocked other clients (mobile, integrations) and made authentication between tiers impossible to enforce.

## Decision

Separate the solution into three projects:

| Project | Role |
|---|---|
| `ResidencyRoll.Api` | ASP.NET Core Web API. Owns EF Core, residency calculation, itinerary parsing, versioned REST endpoints (`/api/v1/trips`). |
| `ResidencyRoll.Web` | Blazor Server UI. Talks to the API through a typed `TripsApiClient`. |
| `ResidencyRoll.Shared` | DTOs and shared extensions used by both. |

The Web app holds no database. CSV import/export moved behind API endpoints.

## Consequences

- The API is the single source of truth for trips and residency rules and can serve additional clients.
- Two container images are built and deployed (`Dockerfile`, `Dockerfile.api`) and run together in `docker-compose.yml`.
- Every Web-to-API call needs an access token (see [ADR-002](ADR-002-auth0-oidc-and-jwt-bearer.md)).
- DTO changes touch three projects.
