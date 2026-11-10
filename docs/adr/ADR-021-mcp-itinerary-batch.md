# ADR-021: Batch Itinerary Writes, Provenance, and a Full Airport Table

## Status

Accepted (2026-11-10). Amends [ADR-016](ADR-016-mcp-server-streamable-http.md) (the `/mcp` body limit is 1 MB) and [ADR-008](ADR-008-timezone-aware-trips-with-iata-airports.md) (the airport table is the OurAirports IATA set, with an IANA timezone for each code).

## Context

A client fixing a real itinerary had to send the same 115-leg batch three times, because Gold Coast (OOL), Canberra (CBR), and Casablanca (CMN) were missing and the first unknown code failed the whole call. Changed flights then took one create and fourteen separate deletes, with no transaction across them. The create response sent every full leg back. Overlapping legs were saved with no signal. Comparing a spreadsheet with the saved trips, and copying trips out, was done by hand. Nothing on a leg said which email it came from. The running server was also an older build, and a client could not ask which commit it was talking to.

## Decision

The airport table is every OurAirports row with an IATA code that is not closed. Timezones come from OpenFlights, and from the airport coordinates when OpenFlights has no zone. The 109 airports already in the table keep their existing city, country, and timezone. A code that is still missing can be saved by passing city, country, and timezone.

`create_trips`, `replace_trips`, and `import_trips_csv` resolve every leg before they write. All of the errors come back in one response, and nothing is saved. `delete_trips(ids)` and `replace_trips(deleteIds, legs)` each run in one transaction. A missing id deletes nothing.

Those batch writes return the ids, a short summary, and any warnings. `detail` `full` includes the legs. `onConflict` is `save` (the default: keep the leg and warn), `reject` (save nothing), or `skip` (leave the overlapping legs out). Overlap is measured in UTC against saved legs and against the other legs in the same call.

`reconcile_trips(legs)` reports legs that are missing, legs whose time or airports differ, and saved legs that are not in the set. A booking reference matches first. Otherwise the same airports and local times match, and the same airports on the same departure date with different times are a difference.

`export_trips` returns CSV in the web export columns and a compact list. `import_trips_csv` accepts that CSV. A Traveller column routes a row. Rows without one use the selected traveller.

A leg may carry `source`, `bookingRef`, and `notes`. They are optional, stored on the trip, included in the CSV, and shown on Manage Trips.

`get_server_info` returns the release, the git commit stamped into the image, and the airport count. The image build passes `github.sha` as `SourceRevisionId`.

## Consequences

- An unknown airport no longer hides the next unknown airport.
- A correction of several legs is one call. The default response stays small enough to read.
- Overlap is visible before the caller has to delete the old legs. `reject` keeps the old legs when the new ones collide.
- A later import can match on `bookingRef` instead of comparing every time by hand.
- The airport file is about 1.5 MB. Search still returns at most 50 matches.
- The home country remains the traveller's saved value. Tool text that talks about browser storage belongs to older builds. `get_server_info` is how a client tells those builds apart.
