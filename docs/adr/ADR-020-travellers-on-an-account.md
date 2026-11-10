# ADR-020: Track Several Travellers on One Account

## Status

Proposed (2026-11-10). Supersedes [ADR-017](ADR-017-home-country-on-the-user-profile.md) once accepted.

## Context

Every trip belongs to the signed-in user (`Trip.UserId`), and the home country lives on that user's profile (ADR-017). A household often needs more than one person's days: a spouse who travels on the same flights but never signs in, or a child. Today the only option is a second login, which the other person would have to create and manage.

The account owner needs to record trips for other people, switch between them in the UI, and ask MCP clients about them. The MCP server is stateless (ADR-016). Every call is independent and several clients, including the React app, act on the same account at once.

A traveller may one day sign in with their own login. That is not built here, but the data model and access checks are shaped so it can be added without moving data or rewriting queries.

## Decision

### Travellers

Add a `Travellers` table owned by an account:

| Column | Notes |
|---|---|
| `Id` | Integer key |
| `OwnerUserId` | The account that owns the traveller and its trips |
| `Name` | Display name, required, unique per owner (case-insensitive), at most 100 characters |
| `HomeCountry` | Nullable, same rules as ADR-017. The MSI minimum in ADR-019 follows the traveller's home country. |
| `LinkedUserId` | Nullable, unique. The login that *is* this person. |
| `CreatedUtc` | Audit |

The account owner's own traveller has `LinkedUserId = OwnerUserId`. There is no `IsSelf` flag: "self" for any signed-in user is the traveller whose `LinkedUserId` is that user. A traveller with no linked login, such as a spouse who never signs in, has `LinkedUserId` null. Linking that traveller later is then a change to one column, and "self" works for the new login without any other change.

`Trip` gains a required `TravellerId`, plus `CreatedByUserId` and `UpdatedByUserId`. `Trip.UserId` stays for now and equals the traveller's `OwnerUserId`, so existing indexes keep working. Access checks stop using `Trip.UserId` and go through the traveller instead (see below).

The home country moves from `UserProfiles` to the traveller. `UserProfiles` keeps any other per-account settings.

### One access check

All reads and writes go through a single `TravellerAccess` service:

- `ResolveSelf(userId)`: the traveller linked to the caller.
- `CanRead(userId, travellerId)` and `CanWrite(userId, travellerId)`.
- `ListAccessible(userId)`.

In this ADR the rule is: a user can read and write a traveller when they own it. No service, controller or MCP tool filters by `UserId` directly. When linked logins arrive, only this service changes.

### Migration

One EF migration, generated with `dotnet ef`:

1. Create `Travellers`.
2. For every distinct `UserId` in `Trips` or `UserProfiles`, insert a traveller with `OwnerUserId` and `LinkedUserId` both set to that user, named from the profile name claim, falling back to "Me", and copy `HomeCountry`.
3. Set `Trip.TravellerId` to that traveller and `CreatedByUserId` to `UserId`, then make `TravellerId` required with a foreign key and an index on `(TravellerId, DepartureDateTime)`.

Counts for existing users do not change.

### API

- `GET /api/v1/travellers`, `POST /api/v1/travellers`, `PUT /api/v1/travellers/{id}`, `DELETE /api/v1/travellers/{id}`.
- Trip, residency, forecast, export and import endpoints take an optional `travellerId` query parameter. Missing means the caller's self traveller, so current clients keep working.
- `GET` and `PUT /api/v1/profile/home-country` act on the self traveller and stay for compatibility. New clients use the traveller endpoints.
- A traveller the caller cannot read returns 404, the same as a trip owned by someone else.
- Deleting a traveller with trips returns 409 unless `?deleteTrips=true` is passed. A traveller linked to the caller cannot be deleted.

### React UI

- A traveller picker in the shell header, defaulting to the self traveller. The last choice is kept in `localStorage` per browser. Nothing on the server records a "current" traveller.
- Every page that shows counts or trips shows the selected traveller's name next to its heading.
- A Travellers page to add, rename, set the home country, and delete.
- Add and edit trip forms can save the same legs for several travellers.

### MCP

There is no server-side "active traveller". A stored selection would be shared by the UI and every MCP client, so switching it in one place would silently change answers, and the target of writes, in another. Each call names its traveller instead.

New tools:

| Tool | Scope | Notes |
|---|---|---|
| `list_travellers` | `read:trips` | Id, name, home country, `isSelf` (computed: linked to the caller) |
| `create_traveller` | `write:trips` | Name and optional home country |
| `update_traveller` | `write:trips` | Rename or change the home country |
| `delete_traveller` | `write:trips` | Refuses while trips exist unless `deleteTrips` is true. Never deletes the caller's self traveller. |
| `copy_trips` | `write:trips` | Copies saved legs to another traveller, skipping legs that already exist there |

Changes to existing tools:

- Every trip, residency, forecast, presence and home-country tool takes an optional `traveller`, by id or name. Missing means the caller's self traveller.
- Names match case-insensitively. No match or several matches return an error that lists the valid travellers.
- `create_trip` and `create_trips` take an optional `travellers` array to save the same legs for several people in one call. The write is atomic across travellers.
- Every result includes `traveller: { id, name }`, and every text summary starts with the traveller's name.
- `get_home_country` and `set_home_country` act on the chosen traveller.

Server instructions add: trips belong to travellers on the signed-in account; omit `traveller` for the signed-in person; when the user names someone or says "we", call `list_travellers` and pass the match; confirm the traveller by name before any write.

Scopes do not change. `read:trips` and `write:trips` cover every traveller the caller can access.

## Designed for a linked login later

Not built in this ADR. Recorded so M11 does not block it. A separate ADR is required before any of this is built.

**Linking.** The owner creates a single-use invite for a traveller. The other person signs in through Auth0 and accepts it, and their user id is written to `LinkedUserId`. Linking never matches on email, because an unverified email claim could take over someone else's traveller.

**What she sees.** Her self traveller is the one linked to her, so the UI and MCP default to her own trips with no other change. By default she sees only her own traveller. The owner can grant her access to others.

**Access.** Replace the owner-only rule in `TravellerAccess` with a `TravellerAccess` table (`TravellerId`, `UserId`, `Role`: `owner`, `editor`, `viewer`). The owner row replaces `OwnerUserId` checks. A linked traveller gets `editor` on itself. MCP tokens for her login see only what her rows allow, with the same scopes.

**Ownership.** A linked person can be given ownership of their own traveller, so their data stays theirs if the household changes. The owner can unlink without deleting data. Unlinking returns the traveller to unlinked and removes her access rows.

**Edits by two people.** `CreatedByUserId` and `UpdatedByUserId`, added now, show who changed a trip. The UI can show "added by" once two people can edit.

**Existing accounts.** If she has already used her own login and saved trips, linking offers to move them onto the traveller, with duplicate legs skipped as in `copy_trips`.

## Consequences

- A household can be tracked from one login. Shared flights are entered once and saved for each traveller.
- Every service method that took a user id now takes a traveller resolved through `TravellerAccess`. This touches `TripService`, `ResidencyQueryService`, the controllers and every MCP tool.
- Exports and backups gain a traveller column. Imports without one go to the self traveller.
- MCP answers can name the wrong person if the model guesses. Naming the traveller in every result and confirming before writes keeps that visible.
- Other travellers' data sits under the owner's account and is visible to anyone the owner shares a login or MCP token with. The owner is responsible for having the other person's agreement.
- Linking a login later changes `LinkedUserId` and the `TravellerAccess` rule. Trips and counts do not move.
