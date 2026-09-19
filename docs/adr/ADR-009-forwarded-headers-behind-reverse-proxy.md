# ADR-009: Configurable Forwarded Headers for Reverse-Proxy Deployments

## Status

Accepted

> Retrospective ADR. Written after the fact from the code and git history; the date is when the decision landed in the repo. Landed 2026-01-13. Setup in [FORWARDED_HEADERS_SETUP.md](../guides/FORWARDED_HEADERS_SETUP.md).

## Context

The app runs behind an NGINX proxy that terminates TLS. Without `X-Forwarded-*` handling, OIDC callback URLs were generated as `http://`, causing redirect mismatches and insecure cookie settings.

## Decision

A shared `UseConfiguredForwardedHeaders()` extension in `ResidencyRoll.Shared` configures ASP.NET Core's ForwardedHeaders middleware for both the API and Web hosts. Trusted proxies and networks come from configuration or environment variables (set in `docker-compose.yml`), and the parsing logs diagnostics. `ForwardedHeadersExtensionsTests` covers it.

## Consequences

- HTTPS-terminating proxies work once trusted proxies or networks are configured.
- Trusting too broad a range lets clients spoof their scheme and IP, so the defaults stay narrow.
