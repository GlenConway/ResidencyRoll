# ADR-012: Blazor Server with Radzen Components and Code-Behind Files

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Radzen chosen 2026-01-01; code-behind convention applied 2026-01-18 (`Refactor Blazor components to use code-behind pattern`).

## Context

A single developer wants an interactive UI in C# without a separate JavaScript build. Charts, data grids, autocomplete and date pickers are needed.

## Decision

- **Blazor Server** (.NET 10) for the Web project.
- **Radzen.Blazor** (community edition) for charts, grids, dialogs and inputs.
- Every component uses the **code-behind pattern**: `.razor` holds markup and directives only, `.razor.cs` holds a `partial class` with parameters, injected services and logic. The rule is recorded in `.github/copilot-instructions.md`.

## Consequences

- Server-side circuits hold per-user state.
- Radzen theming needs CSS overrides (see [ADR-013](ADR-013-carbon-design-system-replaces-liquid-glass.md)).
- Logic in `.razor.cs` files is easier to review and test.
