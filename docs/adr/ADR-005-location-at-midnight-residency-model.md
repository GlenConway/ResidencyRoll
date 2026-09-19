# ADR-005: Location-at-Midnight Daily Presence Model

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-01-17 (`Implement location-at-midnight residency tracking`). Detail in [RESIDENCY_MIDNIGHT_LOGIC.md](../guides/RESIDENCY_MIDNIGHT_LOGIC.md).

## Context

The first version counted days from trip duration (end minus start). That over- and under-counts for overnight flights, short stays, and the International Date Line, and it cannot express rules such as "present at midnight" versus "present any part of the day".

## Decision

Compute residency from a per-calendar-day record, `DailyPresence`:

- `Date`
- `LocationAtMidnight` (country at 00:00 local time)
- `LocationsDuringDay` (every country touched that day)
- `IsInTransitAtMidnight`

Trips are converted into daily presence rows in each leg's own timezone, checking midnight in both departure and arrival zones so International Date Line crossings resolve correctly. The rolling window is 365 days ending today. Per-country day counting is set by [ADR-006](ADR-006-per-country-residency-rules.md).

## Consequences

- Day counts match how tax authorities count days.
- Overlapping trips and date-line legs need explicit handling (`OverlappingTripsTests`, `SydneyAucklandDebugTest`).
- Timeline, dashboard, forecast and CSV export all read the same daily presence data.
- Requires timezone-aware trip data, see [ADR-008](ADR-008-timezone-aware-trips-with-iata-airports.md).
