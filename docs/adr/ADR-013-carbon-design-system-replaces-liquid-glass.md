# ADR-013: Carbon Design System Replaces the Liquid Glass Theme

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Liquid Glass added 2026-01-23; replaced by Carbon 2026-02-19 (`feat: migrate UI from glass effect to Carbon Design System`).

## Context

The Liquid Glass theme used translucent surfaces with WCAG AAA targets. Radzen dropdowns, calendars and autocompletes rendered with transparent backgrounds and unreadable hover states. Roughly 25 commits between 2026-01-23 and 2026-02-03 patched these with high-specificity CSS overrides and `!important` rules.

## Decision

Adopt the IBM Carbon design language through `carbon-theme.css` and `carbon-utils.css` layered over Radzen's base styles, with opaque surfaces, colour tokens and dark-mode support. `accessibility.css` remains for focus and contrast rules.

## Consequences

- The transparency workarounds are gone and inputs use surface tokens.
- The Web project ships its own CSS rather than a Carbon component library, so parity with Carbon is by convention.
