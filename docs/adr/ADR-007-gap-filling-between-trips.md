# ADR-007: Fill Daily Presence Gaps from the Last Arrival

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-01-19 (`Fix gap filling logic and timezone conversion issues`). Rules in [GAP_FILLING_RULES.md](../guides/GAP_FILLING_RULES.md).

## Context

Users enter trips (flights), not every day. Days between trips have no record, so day counts would drop to zero for long stays.

## Decision

A person remains in their arrival country until the next departure. Days between one trip's arrival and the next trip's departure are filled with the arrival location. The rule applies to the timeline, the residency summary and the forecast.

## Consequences

- Users only enter movements.
- A missing or wrong trip propagates forward until the next trip. The timeline makes this visible.
- Before the first trip there is no inferred location.
