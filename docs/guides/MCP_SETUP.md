# Connect an AI client

ResidencyRoll exposes a stateless [Model Context Protocol](https://modelcontextprotocol.io) server at `/mcp` on the same host you already open in the browser. Claude Code, Claude Desktop, claude.ai, Cursor, VS Code, and the MCP Inspector can list trips, check residency days, forecast a stay, and create or update trips for the signed-in user.

The server is the official C# SDK, `ModelContextProtocol.AspNetCore` **2.4.0**. That release speaks protocol revision **2026-07-28** and still accepts the `initialize` handshake used by earlier Streamable HTTP clients, including **2025-11-25**. Design and tool rules are in [ADR-016](../adr/ADR-016-mcp-server-streamable-http.md).

The URL to give a client is `https://<the site you browse>/mcp`. The System Info page shows it, with a Claude Code command and a JSON snippet you can copy.

## Local mode (no login)

When `Jwt:Authority` is unset, `/mcp` is open and every call runs as the local user. The API logs a startup warning. Use this only on a trusted network. It is the same mode as the UI with OpenID Connect turned off.

```bash
claude mcp add --transport http residencyroll http://localhost:8081/mcp
```

`8081` is the web port in `docker-compose.yml`. The compose file used by CI (`docker-compose.build.yml`) publishes the web host on `8080`. A `dotnet run` web host is `https://localhost:5113/mcp`. The API also answers on `https://localhost:5003/mcp` if you can reach it directly.

Open Claude Code and ask, for example, "how many days have I spent in each country in the last 365 days?" No Authenticate step is required in this mode.

## OAuth mode

When JWT authentication is on, `/mcp` answers unauthenticated calls with `401` and a `WWW-Authenticate` header that points at `/.well-known/oauth-protected-resource/mcp`. The client reads that metadata, signs the user in at Auth0 with PKCE, and sends `Authorization: Bearer` on every call. The token's audience must be this MCP server (or the existing API audience). Scopes are `read:trips` and `write:trips`. A missing scope comes back as a tool error that names the scope. Write does not include read.

Set `MCP_RESOURCE_URL` to the public URL clients will use, including the path. For the kinsac deployment that is `https://residencyroll.kinsac.com/mcp`. Leave `MCP_AUDIENCE` empty to use that same URL as the accepted audience. The existing API audience (`https://residencyroll-api.kinsac.com` in that deployment) stays as it is.

### Auth0 (one time)

These steps match the tenant setup applied on 2026-11-09. In the Auth0 dashboard:

1. Create an API named **ResidencyRoll MCP**.
   - Identifier: the MCP resource URL, for example `https://residencyroll.kinsac.com/mcp`.
   - Signing algorithm: RS256.
   - Token encryption: off.
   - Permissions: `read:trips` and `write:trips`.
   - Allow Offline Access: on, so clients can refresh tokens.
2. Under **Settings → Advanced → Settings**, turn on **Resource Parameter Compatibility Profile**. Auth0 still checks `audience` first, then the RFC 8707 `resource` parameter that MCP clients send.
3. Turn on client registration. All of these toggles are in **Settings → Advanced → Settings**:
   - **Dynamic Client Registration (DCR)** lets a client register itself. Third-party applications can sign in only through connections marked domain-level. On the kinsac tenant, `google-oauth2` is domain-level and `Username-Password-Authentication` is not, so self-registered clients sign in with Google.
   - **Client ID Metadata Document (CIMD) Registration** and **Dynamic CIMD Registration** (Early Access). The 2025-11-25 spec prefers CIMD to DCR. Keep DCR on as well, because CIMD is Early Access.
   - **Pre-registered client**, if you would rather not use DCR. Create a Native application (for example "ResidencyRoll – AI clients") and add the callback URLs that client documents. Pass its client id when you add the server. Confirm the current flag with `claude mcp add --help`; it has been `--client-id`.
4. On the ResidencyRoll MCP API, set **Application Access Policy → User-delegated Access** to *All apps allowed*. Under **Default Permissions for third-party applications**, set **User-delegated Access** to *All* and leave Client Access *Unauthorized*. Self-registered clients are third-party apps and otherwise receive no permissions.
5. Leave **Enable Application Connections** unchanged. It only affects applications created later.

### Add the server

Claude Code:

```bash
claude mcp add --transport http residencyroll https://residencyroll.kinsac.com/mcp
```

In the Claude Code session, run `/mcp` and choose **Authenticate**. After you sign in, the tools run as that Auth0 user. A second user sees only their own trips.

Claude Desktop and claude.ai: add a custom connector and paste the same URL.

Cursor: add an MCP server of type HTTP with that URL.

VS Code (`mcp.json`):

```json
{
  "servers": {
    "residencyroll": {
      "type": "http",
      "url": "https://residencyroll.kinsac.com/mcp"
    }
  }
}
```

Clients that read a `mcpServers` file can use the snippet from System Info:

```json
{
  "mcpServers": {
    "residencyroll": {
      "type": "http",
      "url": "https://residencyroll.kinsac.com/mcp"
    }
  }
}
```

MCP Inspector: run `npx @modelcontextprotocol/inspector`, choose Streamable HTTP, and enter the URL. The browser origin of the inspector must be allowed (see settings below) or the API rejects it.

## What the tools can do

Read tools report trips, days per country, the residency summary, one day's location, country rules, forecasts, and the saved home country. `get_residency_summary` and `forecast_trips` use that saved country when `homeCountry` is omitted. Call `set_home_country` (or set it in the app) before answering days left, days over, or days away when none is saved.

`parse_itinerary` is listed only when an OpenAI model, endpoint, and API key are all set. It returns a draft and saves nothing. `lookup_airport` searches the shared airport list.

`create_trip`, `create_trips`, `update_trip`, `delete_trip`, and `set_home_country` change saved data and require `write:trips`. `create_trips` saves every leg or none. Confirm with the user before a write. The server does not treat that confirmation as a security check.

CSV import, backups, and system administration are not exposed.

## Settings

Empty values count as unset.

| Setting | Compose variable | Default |
|---|---|---|
| `Mcp:Enabled` | `MCP_ENABLED` | on |
| `Mcp:ResourceUrl` | `MCP_RESOURCE_URL` | the request host, after forwarded headers |
| `Mcp:Audience` | `MCP_AUDIENCE` | `Mcp:ResourceUrl` when that is set, otherwise only `Jwt:Audience` |
| `Mcp:AllowedOrigins:N` | `MCP_ALLOWED_ORIGIN_0`, `MCP_ALLOWED_ORIGIN_1`, … | none beyond `Cors:AllowedOrigins` |

`/mcp` allows a missing `Origin` (CLI and desktop clients). A browser `Origin` must be in `Cors:AllowedOrigins` or `Mcp:AllowedOrigins`. The endpoint is limited to 120 calls a minute per user, and `parse_itinerary` is limited further. Request bodies over 64 KB are rejected.

## Reverse proxies

The Web host proxies `/mcp` and `/.well-known/oauth-protected-resource` to the API and streams the body. The cluster activity timeout is 5 minutes. nginx in front of the Web host must not buffer that stream:

```nginx
location /mcp {
    proxy_pass http://residencyroll-web;
    proxy_http_version 1.1;
    proxy_buffering off;
    proxy_read_timeout 300s;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
}

location /.well-known/oauth-protected-resource {
    proxy_pass http://residencyroll-web;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
}
```

Caddy's `reverse_proxy` streams response bodies, so it needs no extra directives for `/mcp`. Forwarded headers still have to reach the app so the metadata `resource` URL matches the public host. See [Forwarded Headers Behind a Reverse Proxy](FORWARDED_HEADERS_SETUP.md).
