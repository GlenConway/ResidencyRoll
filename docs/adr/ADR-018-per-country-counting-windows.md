# ADR-018: Count Each Country on Its Own Window

## Status

Accepted (2026-11-09).

## Context

The residency tools counted every country over one rolling 365 days, including both ends. That matches a rolling presence test, and it is what the dashboard asks for when it sends an explicit date range. It is not how every country decides residency.

The UK Statutory Residence Test counts days in the tax year, 6 April to 5 April. A day in the previous tax year must not change this year's UK total. Canada, Australia, New Zealand, and the United States stay on a rolling 365-day window in this app. Other countries can use a calendar year. The rule belongs with the country, next to the midnight or partial-day rule, so summary and forecast use the same period.

A same-day connection also exposed a separate midnight bug: a later leg replaced the location at 00:00. The location for date D is where the person was at 00:00 at the start of D. Later legs the same day do not replace it.

Reaching a threshold counts as residency. A UK count of 183 is residency, not "one day short" and not "zero days over" while the status says over.

## Decision

Each country rule has a `windowType`:

- `rolling365`: the 365 days ending on the reference date, both ends included, so the start is 364 days earlier.
- `taxYear`: from a start month and day through the day before the next anniversary. The United Kingdom, including the `UK` alias, uses 6 April.
- `calendarYear`: 1 January through 31 December of the reference date's year.

Unknown countries stay on the midnight rule, a 183-day threshold, and `rolling365`.

`get_residency_summary` uses those windows when `startDate` is omitted. The windows end on `endDate`, or today. An explicit start and end still count every country over that one range, so the dashboard, which sends its rolling range, does not change. `get_days_per_country` with `last365` stays a rolling 365 days for every country.

`forecast_trips` counts current days on each country's window ending today, and forecast days on each country's window ending on the scenario end. For `rolling365` the peak can be higher than the count at the end of the scenario. For a tax year or calendar year the peak is the period count. An optional `country` limits both lists and adds a short rolling series around that peak. An optional `through` date extends the scenario so saved trips after the last hypothetical leg are included. Optional `excludeTripIds` leaves those saved legs out of the forecast, the peak, the rolling series, and `homeCalendarYears`. Current counts still include them, and the saved legs are not changed.

`max_trip_end_date`, `max_trip_start_date`, and `standard_duration_forecasts` apply the arrival country's window. They accept optional unsaved legs in the same shape as `forecast_trips`. `plan_max_presence` searches the country's window and skips blackout dates. `get_rolling_count` is always the rolling 365-day view, including for a tax-year country, and reports whether the day 365 days earlier counted.

Threshold status is `under`, `approaching` (within 30 days and still under), `reached` (the count equals the threshold), `over` (the count is past it), or `home`. `daysOverThreshold` is 0 until the count is past the threshold. The home country still has null days left and days over.

`locationAtMidnight` for date D is the country at 00:00 at the start of D. Legs are applied in departure order. A later leg does not replace a midnight an earlier leg already set.

## Consequences

- A UK total no longer includes days from the previous tax year unless the caller passes an explicit range that covers them.
- Canada and the other rolling countries keep the same counts they had.
- REST `GET /trips/residency-summary` with no dates follows the same per-country windows. The React dashboard sends both dates, so its range stays rolling.
- `get_daily_presence` returns `{ format, days, runs }` instead of a bare array, so a country filter and a run summary share one schema.
- Reaching 183 is `reached` with `daysOverThreshold` 0. Callers that treated `over` as "at or past the threshold" need to treat `reached` as residency as well.
