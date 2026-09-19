# ADR-014: Keep the Home Country in Browser localStorage

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-01-18 (`persist home country selection in browser localStorage`).

## Context

The dashboard and timeline de-emphasise the home country and report days left/over relative to it. The setting needed to persist without a new API endpoint or schema change.

## Decision

Store the selected home country in `localStorage` through `LocalStorageService` (JS interop). Country colours come from `CountryColorService`.

## Consequences

- The choice is per browser, not per account, so it does not follow the user to another device.
- No API or migration change was needed.
- Moving it to a server-side user profile setting stays open as a future change.
