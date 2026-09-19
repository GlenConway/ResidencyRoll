# ADR-010: Parse Pasted Flight Itineraries with Semantic Kernel and OpenAI

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-02-06 to 2026-02-28. Feature detail in [ITINERARY_PARSING.md](../guides/ITINERARY_PARSING.md).

## Context

Entering multi-leg trips by hand is slow. Users have booking emails and confirmations they can paste.

## Decision

- `ItineraryParsingService` (API) sends pasted text to an OpenAI model through **Microsoft Semantic Kernel** and parses a JSON response into flight legs (`ItineraryFlightLegDto`).
- Endpoint: `POST /api/v1/trips/parse-itinerary`, JWT-protected.
- Settings live in `OpenAIOptions` (model, endpoint, API key), supplied by environment variables (`Add OpenAI env and compose configuration`, 2026-02-28).
- Extracted legs are shown in the UI for review and are saved only on user confirmation. The Manage Trips page has a similar text parser for flight text.

## Consequences

- Itinerary text leaves the deployment and goes to the configured model provider.
- Model output varies, so results are reviewed by the user and errors are returned as a message in the response.
- The feature is optional. Without an API key, manual entry still works.
