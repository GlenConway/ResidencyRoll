# ADR-003: Decrypt Auth0 JWE Access Tokens with the Client Secret

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-01-12.

## Context

Auth0 can issue encrypted access tokens (JWE) for some API configurations. The .NET JWT Bearer middleware rejected them with `IDX10609: Decryption failed` because the API had no decryption key.

## Decision

Support an optional `Jwt:ClientSecret` (env `JWT_CLIENT_SECRET`) on the API. When set, it is used as the token decryption key. In Docker it defaults to the same value as `OIDC_CLIENT_SECRET`. The behaviour and setup steps are documented in [JWT_TOKEN_ENCRYPTION.md](../guides/JWT_TOKEN_ENCRYPTION.md).

## Consequences

- Deployments with signed-only tokens leave the setting empty.
- The API holds a secret that must be supplied through `.env` or user secrets.
- CodeQL flagged clear-text handling of this secret in workflow/config code; those alerts were addressed on 2026-01-13.
