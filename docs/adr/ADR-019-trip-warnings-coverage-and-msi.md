# ADR-019: Warn on Suspect Legs, Report Record Coverage, and Count the Nova Scotia MSI Minimum

## Status

Accepted (2026-11-09). Amended 2026-11-10: each `get_residency_summary` row includes `unaccountedDays`, the days in its window that no country counts. `forecast_trips` returns `homeCalendarYears`, the home country's count in each calendar year the plan touches, with `minimumStatus` when that country has a calendar-year minimum.

## Context

A 30-hour Halifax–London leg and a 4-hour London–Halifax leg were saved with no signal that the times were implausible. The only way to notice was to read the raw legs. Writes also returned the new leg alone, so a correction did not show whether a country's day count moved, and `update_trip` required the whole leg, which made a one-field fix easy to get wrong.

Summaries stop where the saved legs stop. A calendar year that begins before the first trip, or ends after the last one, looks like a full-period total. The presence log does not invent days before the first trip or after the last one.

Nova Scotia MSI eligibility requires 183 days of physical presence in the calendar year. The trip log records countries, not provinces, and the home-country row previously hid every threshold number.

## Decision

Create and update still save the leg. The result includes `warnings` when a leg arrives before it departs in UTC, runs longer than 20 hours, is far from the time suggested by the timezone difference (difference of at least 3 hours, expected time is that difference plus 3 hours, and the leg is under 60 percent or over 180 percent of that), or overlaps another leg. `find_suspect_trips` runs those checks on every saved leg and also flags a gap where the arrival country differs from the next leg's departure country.

`update_trip` applies only the fields that are present. Omitted times and places stay as saved.

`create_trip`, `create_trips`, `update_trip`, and `delete_trip` include `impact`: the per-country change in the rolling 365 days ending today, and in each calendar year whose count changed. A United Kingdom day added in 2024 is reported as United Kingdom +1 in 2024.

`get_record_coverage` returns the earliest local date, the latest local date, and the number of legs. Each `get_residency_summary` row has `coverageStart`, `coverageEnd`, and `isComplete`. The window is complete only when the saved dates cover it from start to end. Days outside that span are not counted. `unaccountedDays` is the number of days in the row's window that no country counts. That includes days before the first saved trip, days after the last one, and a country gap, where one leg arrives in a country and the next leg leaves from a different country. The text names that count when it is greater than zero.

`forecast_trips` returns `homeCalendarYears` for the saved home country. Each entry is that country's day count in one calendar year the hypothetical legs touch, after those legs are included. When the home country has a calendar-year minimum, the entry also has `minimumDays`, `daysShortOfMinimum`, and `minimumStatus`.

Canada, when it is the home country, has `minimumPresenceDays` 183 and `minimumWindowType` calendarYear. The summary keeps `thresholdStatus` `home` for the maximum-days test and adds `daysTowardMinimum`, `daysShortOfMinimum`, and `minimumStatus` (`under`, `approaching`, `reached`) for the MSI minimum. The count is days in Canada.

## Consequences

- A bad leg is saved and called out. The caller decides whether to correct it.
- A summary for calendar 2024 that starts on 19 January, or for calendar 2026 that ends on 23 November, has `isComplete` false and names the covered span.
- The MSI figure is a country count. Days in Canada outside Nova Scotia count toward it, and days in Nova Scotia are not distinguished.
- `create_trip` and `update_trip` return the leg plus `warnings` and `impact`, instead of the leg alone. `delete_trip` adds `impact`.
