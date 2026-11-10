# M11 — Travellers on One Account

Track trips and residency for several people from one login. Design is in [ADR-020](../adr/ADR-020-travellers-on-an-account.md).

## Goals

- The account owner can add travellers (for example a spouse) who never sign in.
- Each traveller has their own trips, counts, and home country.
- The UI and MCP default to the owner, and can act on any traveller by choice.
- Existing data and clients keep working with no change in counts.

## Sub-milestones

### M11.1 — Data model and migration

- `Traveller` entity and `Travellers` table (`OwnerUserId`, `Name`, `HomeCountry`, `LinkedUserId` unique, `CreatedUtc`). No `IsSelf` flag: the self traveller is the one linked to the caller.
- Required `Trip.TravellerId` with foreign key and `(TravellerId, DepartureDateTime)` index, plus `CreatedByUserId` and `UpdatedByUserId`.
- EF migration generated with `dotnet ef` that creates a traveller per existing user with `LinkedUserId = OwnerUserId`, copies the home country, and backfills trips and `CreatedByUserId`.

**Done when:** migration runs on a copy of a production database, every trip has a traveller, and residency counts for existing users match before and after.

### M11.2 — Service layer scoped by traveller

- `TravellerAccess` service (`ResolveSelf`, `CanRead`, `CanWrite`, `ListAccessible`) as the only place that decides access. Rule for now: the caller owns the traveller.
- `TravellerService` (list, get, create, update, delete, resolve by id or name).
- `TripService`, `ResidencyQueryService`, and residency calculation take a traveller scope instead of a bare user id.
- No service, controller, or MCP tool filters by `UserId` directly; everything goes through `TravellerAccess`. A traveller the caller cannot read is treated as not found.
- Trip writes set `CreatedByUserId` and `UpdatedByUserId`.
- Home-country reads and writes move to the traveller.

**Done when:** unit tests cover isolation between two travellers on one account and between two accounts, name resolution (case, ambiguity, no match), refusal to delete the caller's self traveller, and a test that fails if any query filters trips by `UserId` outside `TravellerAccess`.

### M11.3 — REST API

- `/api/v1/travellers` CRUD.
- Optional `travellerId` on trip, residency, forecast, export, and import endpoints, defaulting to self.
- `/api/v1/profile/home-country` kept, acting on self.
- CSV export and backups gain a traveller column. Import without one targets self.
- Swagger updated.

**Done when:** existing API tests pass unchanged, and new tests cover `travellerId` on every endpoint, a 404 for another account's traveller, and the 409 delete guard.

### M11.4 — React UI

- Traveller picker in the shell header, defaulting to self, remembered in `localStorage`.
- Traveller name shown next to page headings on Home, Manage Trips, and Forecast.
- Travellers page: add, rename, home country, delete with confirmation.
- Trip forms can save legs for several travellers.
- Home country UI reads and writes the selected traveller.

**Done when:** Vitest covers the picker and multi-traveller save, and the Playwright smoke adds a second traveller, saves a trip for both, and sees separate counts.

### M11.5 — MCP

- New tools: `list_travellers`, `create_traveller`, `update_traveller`, `delete_traveller`, `copy_trips`.
- Optional `traveller` parameter on every trip, residency, forecast, presence, and home-country tool.
- `travellers` array on `create_trip` and `create_trips`, atomic across travellers.
- `traveller: { id, name }` in every structured result and the name first in every text summary.
- Server instructions updated.
- Output schema validation test (`ToolResults_ShouldMatchTheirOutputSchemas`) extended to the new tools.

**Done when:** MCP tests cover default-to-self, a named traveller, ambiguous and unknown names, a multi-traveller create, and `copy_trips` skipping duplicates; a manual check in Claude Code answers "how many days has my wife spent in Canada" by calling `list_travellers` first.

### M11.6 — Docs

- `docs/guides/MCP_SETUP.md`: traveller parameter and examples.
- `docs/guides/` user guide section on travellers.
- `CLAUDE.md` architecture notes: travellers, home country on the traveller.
- ADR-020 set to Accepted and ADR-017 set to Superseded.

**Done when:** docs merged with the feature PR.

## Out of scope

- Travellers signing in with their own login. ADR-020 records the intended design (invite-based linking, a `TravellerAccess` roles table, ownership transfer, moving trips from an existing account). It needs its own ADR before any work. M11 must not add anything that assumes only one login per traveller set.
- Per-traveller country rules. Rules stay global.
- Per-traveller MCP tokens or scopes.
