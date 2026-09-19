# ADR-011: Docker Images Built and Published by GitHub Actions

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Decision from 2026-01-01, refined through 2026-01-15.

## Context

The project is hosted on GitHub (other Kinsac repos use Azure DevOps pipelines). It ships as self-hosted containers.

## Decision

- Two images, `web` and `api`, built from `Dockerfile` and `Dockerfile.api`. The API listens on 8080 inside the container, and the web port is mapped to 8753 externally.
- `.github/workflows/docker-publish.yml` builds and pushes on `main`, `dev` and `v*` tags, with path filters. The `dev` branch produces dev-tagged images and a `VERSION` build argument stamps the app version.
- `tests.yml` runs `dotnet build` and `dotnet test` (xUnit) on .NET 10. `codeql.yml` runs CodeQL.
- Dependabot updates NuGet (grouped into one weekly PR) and GitHub Actions.
- Branching: feature branches into `dev`, `dev` into `main` through pull requests.

## Consequences

- The registry host is set by the workflow `REGISTRY` env var (currently `kcr.kinsac.com`), while `docker-compose.yml` and the README reference `ghcr.io/glenconway/residencyroll/*`. Keep these aligned when the registry changes.
- Path filters skip builds for docs-only changes.
