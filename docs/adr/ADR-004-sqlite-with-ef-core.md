# ADR-004: SQLite with EF Core, Persisted on a Docker Volume

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Decision from 2026-01-01; migrations reset 2026-01-17.

## Context

ResidencyRoll is a personal, self-hosted tool with a small dataset (trips per user). It should run with `docker compose up` and no external database server.

## Decision

- Use **SQLite** through **Entity Framework Core** in the API (`ApplicationDbContext`).
- Store the file at `./data/residencyroll.db` in development and `/app/data/residencyroll.db` in Docker, on a **named Docker volume** (switched from a bind mount on 2026-01-01).
- Use EF Core migrations. On 2026-01-17 the history was reset to a single `InitialCreate` while the schema was still moving. `RemoveObsoleteColumns` (2026-02-03) dropped the legacy trip fields.
- Remove the unused SQLite store from the Web app when it became a pure API client.

## Consequences

- Zero-dependency deployment and simple backups (copy the volume).
- Single-writer semantics fit the expected load. Scaling to multiple API replicas would need a different provider.
- Test projects use `EntityFrameworkCore.InMemory`.
