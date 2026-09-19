# ADR-006: Per-Country Residency Rules Table

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-01-17; corrected 2026-01-18 (`fix: correct residency rules to match actual tax laws`).

## Context

Countries count days differently. Some count a day only if the person is present at midnight; others count any part of the day. The United States substantial-presence test also excludes certain transit days.

## Decision

`ResidencyCalculationService` holds a table of `CountryResidencyRule` entries keyed by country name (case-insensitive):

| Country | Rule | Threshold |
|---|---|---|
| Canada, Australia, New Zealand | Partial Day | 183 |
| United States (also "USA") | Partial Day, with transit exception | 183 |
| United Kingdom (also "UK") | Midnight | 183 |
| Any other country | Midnight (default) | 183 |

The dashboard compares each country's total against its threshold. The home country is excluded from "days over" warnings and reports days left/over relative to home (`fix: takes into account the home country when showing days left / over`).

## Consequences

- Adding a country is a code change in one dictionary.
- Rules are simplified and are guidance only. They are not tax advice.
- The 183-day threshold is hard-coded for every entry today.
