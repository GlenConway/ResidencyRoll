# ResidencyRoll

[![Tests](https://github.com/GlenConway/ResidencyRoll/actions/workflows/tests.yml/badge.svg)](https://github.com/GlenConway/ResidencyRoll/actions/workflows/tests.yml)
[![CodeQL](https://github.com/GlenConway/ResidencyRoll/actions/workflows/codeql.yml/badge.svg)](https://github.com/GlenConway/ResidencyRoll/actions/workflows/codeql.yml)
[![Docker Build](https://github.com/GlenConway/ResidencyRoll/actions/workflows/docker-publish.yml/badge.svg?branch=main)](https://github.com/GlenConway/ResidencyRoll/actions/workflows/docker-publish.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-brightgreen)](LICENSE)

A travel tracking web application built with ASP.NET Core that helps you track international trips and calculate days spent in each country over a rolling 12-month (365-day) period for residency and tax purposes.

## Table of Contents

- [Architecture](#architecture)
- [Features](#features)
- [MCP tools](#mcp-tools)
- [Tech Stack](#tech-stack)
- [Quick Start](#quick-start)
  - [Docker Deployment (Recommended)](#docker-deployment-recommended)
  - [Local Development](#local-development)
- [Configuration](#configuration)
  - [First-Time Setup](#first-time-setup)
  - [Authentication Setup](#authentication-setup)
- [API Documentation](#api-documentation)
- [Project Structure](#project-structure)
- [How It Works](#how-it-works)
- [Deployment](#deployment)
- [Troubleshooting](#troubleshooting)
- [License](#license)

## Architecture

ResidencyRoll uses a modern **API-first architecture** with separated concerns:

- **API Backend** ([ResidencyRoll.Api](src/ResidencyRoll.Api/)): ASP.NET Core Web API with:
  - RESTful endpoints with versioning (`/api/v1/trips`)
  - JWT Bearer token authentication
  - OpenAPI/Swagger documentation
  - SQLite database with Entity Framework Core
  
- **Web Frontend** ([ResidencyRoll.Web](src/ResidencyRoll.Web/)): React SPA served by an ASP.NET Core BFF with:
  - Tailwind CSS and shadcn
  - OpenID Connect cookie authentication (tokens stay on the server)
  - A reverse proxy that forwards `/api` with the user's access token, and `/mcp` with the AI client's own token

- **Shared Models** ([ResidencyRoll.Shared](src/ResidencyRoll.Shared/)): Common DTOs, the airport list, and MCP setting helpers used by both projects

Trip and residency logic live in the API. The web app and MCP clients both call it.

## Features

- **Residency dashboard**: Country cards for a date range you choose, defaulting to the last 365 days. Each card shows the days counted, whether that country uses the midnight or partial-day rule, the day threshold, and progress toward it. The home country is saved on the traveller you are looking at and changes the days-left and days-over wording.
- **Travellers**: One login can track several people. The header chooses who you are looking at, and that choice stays in this browser. Each person has their own trips, day counts, and home country. A new trip can be saved for more than one person at once. See [Travellers on one account](docs/guides/TRAVELLERS.md).
- **Daily presence timeline**: One cell per day, grouped by month and coloured by country, including days spent in transit. Select a day to see the midnight location and the countries visited. The same range can be exported as CSV.
- **Trip management**: Add, edit, and delete flight legs, with airports and local departure and arrival times. Filter the list, import and export trips as CSV, and paste a booking to draft legs when itinerary parsing is configured.
- **Forecast**: Plan a future trip of one or more legs and compare days in the last 365 days with the projected window. The forecast is not saved.
- **Backups**: System Info shows the database, user and trip counts, and the next weekly backup. You can take a backup immediately and download it.
- **Authentication**: An OpenID Connect cookie on the web app and a JWT bearer token on the API. Authentication is off by default for local development.
- **AI clients**: A Streamable HTTP MCP server at `/mcp`. The tools are listed below. Setup is in [Connect an AI client](docs/guides/MCP_SETUP.md).
- **Storage**: SQLite, mapped to a Docker volume when the app is run with Docker Compose.

## MCP tools

The server is on the web host at `/mcp`. Read tools need the `read:trips` scope. Write tools need `write:trips`, and write does not include read. When JWT authentication is off, calls run as the local user. A call is about the signed-in person unless you pass `traveller` (an id or a name). The home country is saved on that traveller. `get_residency_summary` and `forecast_trips` use it when `homeCountry` is omitted.

| Tool | What it does |
|---|---|
| `list_trips` | Flight legs, optionally limited to those that overlap a date range |
| `get_trip` | One leg by id |
| `get_days_per_country` | Days per country for the last 365 days, or for the whole log |
| `get_residency_summary` | Days, threshold status (`under`, `approaching`, `reached`, `over`, or `home`), days left, and days over. Says when the window runs past the saved trips, and how many days in the window no country counts. When Canada is home, also shows the 183-day calendar-year minimum |
| `get_daily_presence` | Midnight location for each day, or contiguous runs. `locationAtMidnight` is 00:00 at the start of that date. Optional country filter. Up to 366 days per call |
| `get_rolling_count` | Rolling 365-day total for one country on each day, and whether the anniversary day counted |
| `get_location_on` | The presence record for a single date |
| `get_current_location` | Current country and the last arrival airport |
| `get_country_rules` | Midnight or partial-day rule, threshold, counting window, and any home-country minimum |
| `get_record_coverage` | Earliest saved date, latest saved date, and the number of legs |
| `find_suspect_trips` | Legs whose duration, order, overlap, or country sequence looks wrong |
| `get_home_country` | The home country saved on the chosen traveller |
| `set_home_country` | Save or clear that traveller's home country |
| `list_travellers` | The people on this account: id, name, home country, and whether the row is you |
| `create_traveller` | Add a person, with an optional home country |
| `update_traveller` | Rename a person or change their home country |
| `delete_traveller` | Delete a person. Refuses you, and refuses someone who still has trips unless `deleteTrips` is true |
| `copy_trips` | Copy saved legs from one person to another, skipping legs that are already there |
| `get_server_info` | The release, git commit, and airport-table size of the build that answered |
| `lookup_airport` | Search the shared airport list by IATA code, city, name, or country |
| `forecast_trips` | Projected days if the hypothetical legs were added. Nothing is saved. Optional country filter, a through date, a short rolling count around the peak, and the home country's calendar-year count for each year the plan touches |
| `max_trip_end_date` | Last midnight you can still be in the arrival country while its count stays at or under a day limit (183 by default). Optional unsaved legs |
| `max_trip_start_date` | Earliest arrival that stays at or under the day limit when the departure date is fixed |
| `plan_max_presence` | Arrival and departure dates that add the most days in a country without passing the limit, skipping blackout dates |
| `standard_duration_forecasts` | Days in the arrival country for stay lengths such as 7, 14, 30, 60, 90, and 180 days. Optional unsaved legs |
| `parse_itinerary` | Draft legs from pasted booking text. Listed only when an OpenAI model, endpoint, and API key are set. Nothing is saved |
| `create_trip` | Save one leg. An optional `travellers` list saves that same leg for several people in one transaction. Returns warnings and the change in country day counts. `onConflict` can reject or skip an overlapping leg |
| `update_trip` | Change one leg. Omitted fields stay as they are. Returns warnings and the day-count change |
| `delete_trip` | Delete one leg and report the day-count change |
| `delete_trips` | Delete every listed leg, or none of them, in one transaction |
| `create_trips` | Save every leg, or none of them, in one transaction. Checks every leg first and returns every error together. The default result is ids, a short summary, and warnings |
| `replace_trips` | Delete one set of legs and save another in the same transaction |
| `reconcile_trips` | Compare a set of legs with the saved trips: missing, different times, and saved legs that are not in the set |
| `export_trips` | CSV in the web export format, plus a compact list of airports, times, source, and booking reference |
| `import_trips_csv` | Import that same CSV. A bad row saves nothing and every error comes back together |

There is also a `residency_check` prompt and a `residencyroll://rules/countries` resource. Backups and system administration are not exposed as tools. Connection steps, OAuth, and rate limits are in [Connect an AI client](docs/guides/MCP_SETUP.md).

## Tech Stack

- **Backend**: ASP.NET Core Web API (.NET 10)
- **Frontend**: React 19, TypeScript, and Vite, served by ASP.NET Core (.NET 10)
- **Authentication**: OpenID Connect cookie on the BFF, JWT Bearer on the API
- **UI**: Tailwind CSS and shadcn
- **Database**: SQLite with Entity Framework Core
- **API Documentation**: Swagger/OpenAPI
- **Deployment**: Docker Compose with multi-container setup

## Quick Start

### Docker Deployment (Recommended)

The fastest way to run the application with persistent data.

#### Using Pre-built Images (Production)

1. **Download the required files:**

   ```bash
   # Download docker-compose.yml and .env.example
   curl -O https://raw.githubusercontent.com/GlenConway/ResidencyRoll/main/docker-compose.yml
   curl -O https://raw.githubusercontent.com/GlenConway/ResidencyRoll/main/.env.example
   ```

2. **Configure environment variables:**

   ```bash
   # Copy the example file
   cp .env.example .env
   
   # Edit .env with your configuration
   nano .env
   ```

   At minimum, configure these settings in `.env`:

   ```bash
   # Port Configuration (defaults are fine for most setups)
   API_PORT=8080
   WEB_PORT=8081
   
   # Enable authentication (optional - set to false for no auth)
   OIDC_ENABLED=false
   
   # If OIDC_ENABLED=true, configure your identity provider:
   OIDC_AUTHORITY=https://your-tenant.auth0.com/
   OIDC_CLIENT_ID=your-client-id
   OIDC_CLIENT_SECRET=your-client-secret
   JWT_AUTHORITY=https://your-tenant.auth0.com/
   JWT_AUDIENCE=your-api-identifier
   ```

   See `.env.example` for all available configuration options.

3. **Start the services:**

   ```bash
   docker compose up -d
   ```

Access the application:

- **Web UI**: `http://localhost:8081`
- **API**: `http://localhost:8080`
- **API Swagger**: `http://localhost:8080/swagger`

#### Environment Variables

The docker-compose.yml uses environment variables from the `.env` file for all configuration:

| Variable | Description | Default |
|----------|-------------|---------|
| `API_PORT` | External port for API | `8080` |
| `API_INTERNAL_PORT` | Internal container port for API | `80` |
| `WEB_PORT` | External port for Web UI | `8081` |
| `WEB_INTERNAL_PORT` | Internal container port for Web | `8080` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Production` |
| `DB_PATH` | SQLite database path | `/app/data/residencyroll.db` |
| `OIDC_ENABLED` | Enable/disable authentication | `false` |
| `OIDC_AUTHORITY` | Identity provider URL | - |
| `OIDC_CLIENT_ID` | Web app client ID | - |
| `OIDC_CLIENT_SECRET` | Web app client secret | - |
| `JWT_AUTHORITY` | JWT issuer URL | - |
| `JWT_AUDIENCE` | JWT audience identifier | - |
| `CORS_ORIGIN_0` | Allowed CORS origin 1 | `http://localhost:8081` |
| `CORS_ORIGIN_1` | Allowed CORS origin 2 | `http://residencyroll-web:80` |
| `FORWARDED_HEADERS_KNOWN_PROXY_0` | Trusted reverse proxy IP address | - |
| `FORWARDED_HEADERS_KNOWN_PROXY_1` | Additional trusted proxy IP | - |
| `FORWARDED_HEADERS_KNOWN_NETWORK_0` | Trusted proxy network in CIDR notation | - |
| `FORWARDED_HEADERS_KNOWN_NETWORK_1` | Additional trusted network | - |

#### Reverse Proxy Configuration

If you deploy ResidencyRoll behind a reverse proxy (nginx, Caddy, cloud load balancer), you must configure trusted proxies to ensure the application correctly processes `X-Forwarded-For` and `X-Forwarded-Proto` headers. This is critical for:

- Correctly identifying client IP addresses in logs
- Proper HTTPS redirect behavior
- Security (preventing header spoofing attacks)

**Default Behavior**: Without configuration, ASP.NET Core only trusts localhost/loopback addresses, which is secure for direct deployments.

**When to Configure**:

- ✅ Using nginx, Caddy, Traefik, or cloud load balancers
- ✅ Containers behind Docker network or Kubernetes ingress
- ✅ Any multi-tier deployment with a reverse proxy layer

**Configuration Options**:

1. **Known Proxies** (specific IP addresses):

   ```bash
   # Single nginx proxy
   FORWARDED_HEADERS_KNOWN_PROXY_0=172.17.0.1
   
   # Multiple proxies
   FORWARDED_HEADERS_KNOWN_PROXY_0=172.17.0.1
   FORWARDED_HEADERS_KNOWN_PROXY_1=10.0.1.5
   ```

2. **Known Networks** (CIDR ranges for dynamic IPs):

   ```bash
   # Docker bridge network
   FORWARDED_HEADERS_KNOWN_NETWORK_0=172.17.0.0/16
   
   # Cloud load balancer subnet
   FORWARDED_HEADERS_KNOWN_NETWORK_0=10.240.0.0/16
   
   # Private network range
   FORWARDED_HEADERS_KNOWN_NETWORK_0=10.0.0.0/8
   ```

**Example nginx Configuration**:

```nginx
server {
    listen 80;
    server_name residencyroll.example.com;
    
    location / {
        proxy_pass http://localhost:8081;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Then configure the proxy's IP in `.env`:

```bash
FORWARDED_HEADERS_KNOWN_PROXY_0=172.17.0.1  # nginx container IP
```

**Security Warning**: Only add IP addresses/networks you control. Misconfiguration can allow attackers to spoof headers and bypass security controls.

#### Data Persistence

Docker volumes ensure data persists across container restarts:

- `residencyroll-api-data` - SQLite database
- `residencyroll-web-data` - Web application data

To backup your data:

```bash
# Export the database
docker compose exec residencyroll-api cat /app/data/residencyroll.db > backup.db

# Restore from backup
docker compose cp backup.db residencyroll-api:/app/data/residencyroll.db
```

To completely remove all data:

```bash
docker compose down -v  # The -v flag removes volumes
```

#### Building from Source

```bash
# Clone and build locally
git clone https://github.com/GlenConway/ResidencyRoll.git
cd ResidencyRoll

docker compose -f docker-compose.build.yml up -d --build
```

### Local Development

#### Quick Start (No Authentication)

1. **Start the API:**

   ```bash
   cd src/ResidencyRoll.Api
   dotnet watch run
   ```

2. **Start the Web app** (in a new terminal):

   ```bash
   cd src/ResidencyRoll.Web
   dotnet watch run
   ```

3. **Access**: the web app at `https://localhost:5113` and the API at `https://localhost:5003` (Swagger at `https://localhost:5003/swagger`).

For the Vite dev server, in a third terminal:

```bash
npm run dev --prefix src/ResidencyRoll.Web/ClientApp
```

That UI is at `http://127.0.0.1:5173` and proxies `/api` through the web host.

Authentication is **disabled by default** for local development.

#### First-Time Setup (Full Configuration)

For complete setup including authentication:

```bash
# Copy example configuration files
cp src/ResidencyRoll.Web/appsettings.Development.json.example src/ResidencyRoll.Web/appsettings.Development.json
cp src/ResidencyRoll.Api/appsettings.Development.json.example src/ResidencyRoll.Api/appsettings.Development.json
```

See [Configuration](#configuration) section below for authentication setup.

## Configuration

### First-Time Setup

Configuration files are excluded from source control to protect secrets.

1. **Copy example files:**

   ```bash
   cp src/ResidencyRoll.Web/appsettings.Development.json.example src/ResidencyRoll.Web/appsettings.Development.json
   cp src/ResidencyRoll.Api/appsettings.Development.json.example src/ResidencyRoll.Api/appsettings.Development.json
   ```

2. **Verify files are ignored:**

   ```bash
   git status --ignored | grep appsettings
   ```

These files are in `.gitignore` and will NOT be committed.

### Authentication Setup

Authentication is **disabled by default** for development. To enable:

#### Supported Identity Providers

- Azure AD / Microsoft Entra ID
- Auth0 (easiest for testing)
- Keycloak (self-hosted)
- Okta, Duende IdentityServer
- Any OIDC-compliant provider

#### Auth0 Quick Setup (Recommended for Testing)

##### 1. Create Auth0 Account

- Sign up at [auth0.com](https://auth0.com)
- Create a tenant (e.g., `your-tenant-name`)
- Your Authority URL: `https://your-tenant-name.auth0.com/`

##### 2. Create API in Auth0

- Dashboard → **Applications → APIs** → **Create API**
- Name: `ResidencyRoll API`
- Identifier: `https://api.residencyroll.com` (or any unique value)
- Signing Algorithm: RS256
- **Save the Identifier** - this is your **Audience**

##### 3. Create Web Application

- Dashboard → **Applications → Applications** → **Create Application**
- Name: `ResidencyRoll Web`
- Type: **Regular Web Application**
- Settings:
  - Allowed Callback URLs: `https://localhost:5113/signin-oidc`
  - Allowed Logout URLs: `https://localhost:5113/`
  - Allowed Web Origins: `https://localhost:5113`
- **Copy**: Domain, Client ID, Client Secret

##### 4. Configure Your Application

Choose one of these methods:

##### Option A: User Secrets (Most Secure - Recommended)

```bash
# Web App
cd src/ResidencyRoll.Web
dotnet user-secrets set "Authentication:OpenIdConnect:Authority" "https://YOUR-TENANT.auth0.com/"
dotnet user-secrets set "Authentication:OpenIdConnect:ClientId" "YOUR-CLIENT-ID"
dotnet user-secrets set "Authentication:OpenIdConnect:ClientSecret" "YOUR-CLIENT-SECRET"

# API
cd ../ResidencyRoll.Api
dotnet user-secrets set "Jwt:Authority" "https://YOUR-TENANT.auth0.com/"
dotnet user-secrets set "Jwt:Audience" "YOUR-API-IDENTIFIER"
```

Or use the automated configuration script:

```bash
./configure-auth0.sh
```

##### Option B: Edit Configuration Files

**Web** (`src/ResidencyRoll.Web/appsettings.Development.json`):

```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "Authority": "https://YOUR-TENANT.auth0.com/",
      "ClientId": "YOUR-CLIENT-ID",
      "ClientSecret": "YOUR-CLIENT-SECRET",
      "RequireHttpsMetadata": false,
      "ApiScope": "openid profile email"
    }
  }
}
```

**API** (`src/ResidencyRoll.Api/appsettings.Development.json`):

```json
{
  "Jwt": {
    "Authority": "https://YOUR-TENANT.auth0.com/",
    "Audience": "YOUR-API-IDENTIFIER",
    "RequireHttpsMetadata": false
  }
}
```

⚠️ **Important**: Never commit secrets! Files are gitignored but run `git restore` if you edit them directly.

##### 5. Test Authentication

1. Start both services
2. Open `https://localhost:5113`
3. Click **Login** button
4. Authenticate with Auth0
5. You should see your name in the top-right corner

#### Other Identity Providers

##### Azure AD / Microsoft Entra ID

**API configuration:**

```json
{
  "Jwt": {
    "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
    "Audience": "api://residencyroll-api"
  }
}
```

**Web configuration:**

```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "ClientId": "{web-app-client-id}",
      "ClientSecret": "{web-app-client-secret}",
      "ApiScope": "api://residencyroll-api/.default"
    }
  }
}
```

##### Keycloak

**API configuration:**

```json
{
  "Jwt": {
    "Authority": "https://keycloak.example.com/realms/{realm-name}",
    "Audience": "residencyroll-api"
  }
}
```

**Web configuration:**

```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "Authority": "https://keycloak.example.com/realms/{realm-name}",
      "ClientId": "residencyroll-web",
      "ClientSecret": "{your-client-secret}",
      "ApiScope": "residencyroll-api"
    }
  }
}
```

#### Production Deployment with Authentication

1. **Create `.env` file** from the example:

   ```bash
   cp .env.example .env
   ```

2. **Configure authentication** in `.env`:

   ```bash
   # Enable authentication
   OIDC_ENABLED=true
   
   # Identity Provider Configuration
   OIDC_AUTHORITY=https://your-tenant.auth0.com/
   OIDC_CLIENT_ID=residencyroll-web
   OIDC_CLIENT_SECRET=your-secret-here
   OIDC_REQUIRE_HTTPS=true
   OIDC_API_SCOPE=residencyroll-api
   
   # API JWT Configuration
   JWT_AUTHORITY=https://your-tenant.auth0.com/
   JWT_AUDIENCE=residencyroll-api
   JWT_REQUIRE_HTTPS=true
   
   # Update CORS origins for your domain
   CORS_ORIGIN_0=https://your-domain.com
   CORS_ORIGIN_1=http://residencyroll-web:80
   ```

3. **Deploy with Docker Compose**:

   ```bash
   docker compose up -d
   ```

4. **Security Checklist**:
   - [ ] Set `OIDC_ENABLED=true` in production
   - [ ] Set `JWT_REQUIRE_HTTPS=true` and `OIDC_REQUIRE_HTTPS=true`
   - [ ] Use HTTPS for all endpoints (configure reverse proxy like nginx or Caddy)
   - [ ] Store `.env` file securely (never commit it to git)
   - [ ] Configure proper CORS origins (no wildcards)
   - [ ] Set appropriate token expiration times in your identity provider
   - [ ] Enable security headers (HSTS, CSP, etc.) via reverse proxy
   - [ ] Regularly update Docker images: `docker compose pull && docker compose up -d`

## API Documentation

When running the API, Swagger UI is available at:

- `https://localhost:5003/swagger` (local development)
- `http://localhost:8080/swagger` (Docker)

The API supports versioning (`/api/v1/trips`) and includes comprehensive OpenAPI documentation.

### Testing API Manually

```bash
# Get an access token from your identity provider
TOKEN="your-jwt-token-here"

# Call API endpoint
curl -X GET "http://localhost:8080/api/v1/trips" \
  -H "Authorization: Bearer $TOKEN"
```

## Project Structure

```text
ResidencyRoll/
├── src/
│   ├── ResidencyRoll.Api/               # API Backend
│   │   ├── Controllers/                 # Trips, travellers, profile, system
│   │   ├── Mcp/                         # /mcp tools, OAuth metadata, transport guards
│   │   ├── Services/                    # Trips, travellers, residency
│   │   ├── Data/
│   │   │   └── ApplicationDbContext.cs  # EF Core DbContext
│   │   ├── Models/                      # Trip, Traveller
│   │   └── Program.cs                   # API configuration
│   │
│   ├── ResidencyRoll.Web/               # BFF and React UI
│   │   ├── Bff/                         # Auth, CSRF, and the API proxy
│   │   ├── ClientApp/                   # Vite React application
│   │   └── Program.cs                   # Web host configuration
│   │
│   ├── ResidencyRoll.Shared/            # Shared DTOs and the airport list
│   └── ResidencyRoll.Tests/             # xUnit tests
│
├── Dockerfile                           # Web container
├── Dockerfile.api                       # API container
├── docker-compose.yml                   # Multi-container orchestration
└── README.md                            # This file
```

## How It Works

### Rolling 365-day calculation

Each flight leg has a departure airport, an arrival airport, and local times. Those legs become a daily presence record: the country at local midnight, the countries visited during the day, and whether midnight was spent in transit. Gaps between trips stay in the last arrival country.

A day counts for the country where you were at midnight. Partial-day countries, such as the United States substantial-presence test, also count a day when you were there for only part of it. The usual window is the 365 days ending today, and the usual threshold is 183 days. The home country is stored on the traveller. `GET` and `PUT /api/v1/profile/home-country` still read and write the signed-in person's traveller.

See [Residency midnight logic](docs/guides/RESIDENCY_MIDNIGHT_LOGIC.md) and [Gap filling](docs/guides/GAP_FILLING_RULES.md).

### Data management

Manage Trips lists the legs for the person selected in the header. **Add Trip** opens the editor. When the account has more than one person, the editor can save the same leg for each person you check. **Edit** replaces a leg, and **Delete** asks for confirmation. **Import / Export** downloads a CSV or uploads one. An export adds a Traveller column at the end. An older file without that column imports onto the person selected in the header. Changes are written to SQLite immediately.

### Docker Volume

The API database is the named Docker volume `residencyroll-api-data`, mounted at `/app/data` in the API container.

## Deployment

### GitHub Container Registry (GHCR)

The project uses GitHub Actions to automatically build and push Docker images to GHCR on every push to `main`.

**Using pre-built images:**

```yaml
services:
  residencyroll-api:
    image: ghcr.io/glenconway/residencyroll/api:latest
    # ... configuration
  
  residencyroll-web:
    image: ghcr.io/glenconway/residencyroll/web:latest
    # ... configuration
```

### Versioning

Both API and Web containers support versioning:

```bash
# Build with specific version
docker build -f Dockerfile.api --build-arg VERSION=1.2.3 -t residencyroll-api:1.2.3 .

# Using git tags in CI/CD
git tag v1.2.3
git push origin v1.2.3  # Triggers versioned build
```

The version is logged on startup:

```text
[INF] Starting ResidencyRoll API - Version: 1.2.3
```

### Database Backup & Restore

Copy the SQLite file before upgrading a database that already has trips. That upgrade adds a traveller for each account and attaches the existing trips. Locally the file is under `./data`. In Docker it is `/app/data`. See [Travellers on one account](docs/guides/TRAVELLERS.md).

**Backup:**

```bash
docker run --rm -v residencyroll-api-data:/data -v $(pwd):/backup \
  alpine tar czf /backup/api-backup.tar.gz -C /data .
```

**Restore:**

```bash
docker run --rm -v residencyroll-api-data:/data -v $(pwd):/backup \
  alpine tar xzf /backup/api-backup.tar.gz -C /data
```

### Deployment Environment Variables

All configuration can be set via environment variables:

**API:**

- `JWT_AUTHORITY` - Identity provider URL
- `JWT_AUDIENCE` - API identifier
- `JWT_REQUIRE_HTTPS` - HTTPS enforcement (true/false)
- `ConnectionStrings__Default` - Database connection string

**Web:**

- `OIDC_ENABLED` - Enable authentication (true/false)
- `OIDC_AUTHORITY` - Identity provider URL
- `OIDC_CLIENT_ID` - Client identifier
- `OIDC_CLIENT_SECRET` - Client secret
- `OIDC_REQUIRE_HTTPS` - HTTPS enforcement (true/false)
- `OIDC_API_SCOPE` - API scope to request
- `Api__BaseUrl` - API base URL

## Troubleshooting

### Docker Issues

```bash
# View logs
docker compose logs -f
docker compose logs -f residencyroll-api
docker compose logs -f residencyroll-web

# Check container status
docker compose ps

# Access container shell
docker exec -it ResidencyRoll-Api /bin/bash

# Rebuild and restart
docker compose down -v
docker compose up --build
```

### Local Development Issues

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build

# Check .NET 10 SDK is installed
dotnet --version
```

### Authentication Issues

| Issue | Solution |
|-------|----------|
| 401 Unauthorized from API | Check JWT Authority and Audience match between API and identity provider |
| "IDX10609: Decryption failed" | Auth0 is issuing encrypted tokens (JWE). Configure `JWT_CLIENT_SECRET` with the same value as `OIDC_CLIENT_SECRET`. See [JWT Token Encryption Guide](docs/guides/JWT_TOKEN_ENCRYPTION.md) |
| Login redirect loop | Verify redirect URIs are registered in identity provider |
| Token not forwarded | Check `ApiAuthenticationHandler` is registered; verify `SaveTokens: true` in OIDC options |
| CORS errors | Add Web URL to API CORS AllowedOrigins |
| Certificate errors (dev) | Set `RequireHttpsMetadata: false` in development configuration |

### Database Location

- **Docker**: `/var/lib/docker/volumes/residencyroll-api-data/_data/residencyroll.db`
- **Local**: `./data/residencyroll.db` (created automatically)

To reset the database, delete the file or remove the Docker volume:

```bash
docker compose down -v
```

## License

MIT
