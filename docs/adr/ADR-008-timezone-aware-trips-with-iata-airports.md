# ADR-008: Timezone-Aware Trips Resolved from IATA Airport Codes

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-01-17 (`Implement IATA autocomplete and timezone resolution`, `Remove legacy data support and simplify UI for timezone-aware trips`).

## Context

[ADR-005](ADR-005-location-at-midnight-residency-model.md) needs the local date and time at each end of a flight. Entering only country and date cannot answer that.

## Decision

- A trip is a **leg**: departure airport, departure local date/time, arrival airport, arrival local date/time.
- Airports come from a bundled dataset (`AirportData`: IATA code, name, city, country, IANA timezone), sourced from OpenFlights and validated against the IANA database. `AirportSelector` gives autocomplete.
- Timezones are IANA identifiers resolved through `TimeZoneInfo`.
- Legacy country-only trip fields were removed (migration `RemoveObsoleteColumns`, 2026-02-03).
- "Save and add leg" pre-fills the next leg from the prior arrival to support multi-leg journeys.

## Consequences

- Duration and midnight calculations are exact per leg.
- The airport list is static and needs a manual refresh for new airports or timezone changes.
- Timezone handling caused several regressions, which are covered by `ForecastWithTimezonesTests`, `TripTimePersistenceTests` and `TripDurationTests`.
