# ADR-017: Store the Home Country on the User Profile

## Status

Accepted (2026-11-09). Supersedes [ADR-014](ADR-014-home-country-in-browser-storage.md).

## Context

Days left, days over, and days away depend on which country is home. That choice lived in browser `localStorage`, so it did not follow the account to another device and MCP clients could not see it. The tools took an optional `homeCountry` argument and told the model to ask every time.

## Decision

Store one home country per user in `UserProfiles`, keyed by the same user id as trips. The React app reads and writes it through `GET` and `PUT /api/v1/profile/home-country`. On first load, a value that exists only in `localStorage` is copied to the profile. The server value wins after that.

MCP adds `get_home_country` and `set_home_country`. `get_residency_summary` and `forecast_trips` use the saved country when their `homeCountry` argument is omitted. An explicit argument still overrides the saved value for that call and does not change the profile.

## Consequences

- MCP can answer days left, days over, and days away without asking once the user has chosen a home country in the app or through `set_home_country`.
- The choice follows the account. A browser that still has an older `localStorage` value adopts the profile value on the next visit.
- The alphabetical display fallback in the dashboard is not saved. MCP sees a home country only after an explicit choice.
- `set_home_country` requires `write:trips`. Reading the saved country requires `read:trips`.
