# ADR-002: Auth0 OpenID Connect for the Web App, JWT Bearer for the API

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-01-11 to 2026-01-12.

## Context

After [ADR-001](ADR-001-split-api-and-web.md) the API needed to identify the caller and scope trips per user. The app is self-hosted, so it needs an external identity provider and no local user store.

## Decision

- **Web:** OpenID Connect (authorization code) against an OIDC provider, Auth0 in the reference setup (`configure-auth0.sh` provisions it). The Web app requests an access token for the API audience.
- **API:** `JwtBearer` authentication validates the token (authority, audience). Endpoints use `[Authorize]`.
- **Token forwarding:** `ApiAuthenticationHandler` and `AccessTokenProvider` attach the signed-in user's access token to outgoing API calls.
- **Ownership:** Trips carry the user id from the token (`feat: link trips to logged-in user`), and queries filter on it.
- Provider settings come from environment variables / user secrets. `appsettings.Development.json` is git-ignored with `.example` templates checked in.

## Consequences

- No passwords or user tables in the application.
- Any OIDC provider works if it issues JWT access tokens for an API audience.
- Client secrets must never be committed; a compose file leak was fixed on 2026-01-17 (`fix/remove-jwt-client-secret-from-docker-compose`).
- Encrypted access tokens need extra configuration, see [ADR-003](ADR-003-jwe-token-decryption.md).
