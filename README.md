# ResidencyRoll

[![Tests](https://github.com/GlenConway/ResidencyRoll/actions/workflows/tests.yml/badge.svg)](https://github.com/GlenConway/ResidencyRoll/actions/workflows/tests.yml)
[![CodeQL](https://github.com/GlenConway/ResidencyRoll/actions/workflows/codeql.yml/badge.svg)](https://github.com/GlenConway/ResidencyRoll/actions/workflows/codeql.yml)
[![Docker Build](https://github.com/GlenConway/ResidencyRoll/actions/workflows/docker-publish.yml/badge.svg?branch=main)](https://github.com/GlenConway/ResidencyRoll/actions/workflows/docker-publish.yml)
[![License](https://img.shields.io/github/license/GlenConway/ResidencyRoll.svg)](LICENSE)

A travel tracking web application built with ASP.NET Core that helps you track international trips and calculate days spent in each country over a rolling 12-month (365-day) period for residency and tax purposes.

## Table of Contents

- [Architecture](#architecture)
- [Features](#features)
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
  
- **Web Frontend** ([ResidencyRoll.Web](src/ResidencyRoll.Web/)): Blazor Server application with:
  - Interactive UI using Radzen components
  - OpenID Connect authentication
  - Typed HTTP client for API communication
  - Token forwarding to backend API

- **Shared Models** ([ResidencyRoll.Shared](src/ResidencyRoll.Shared/)): Common DTOs used by both projects

This separation allows the API to be consumed by multiple clients (web, mobile, third-party integrations) while maintaining a single source of truth for business logic.

## Features

- **Rolling 365-Day Calculations**: Automatically calculates days spent in each country within the last 365 days from today
- **Visual Dashboard**:
  - Arc Gauge showing Days Away vs Days at Home
  - Donut Chart displaying country distribution
  - Interactive Timeline of all trips
- **Trip Management**: Inline CRUD operations using Radzen DataGrid
- **Forecast Tool**: Plan future trips and see projected day counts
- **Authentication**: JWT/OAuth/OpenID Connect support for secure API access
- **API-First Design**: RESTful API ready for mobile apps and integrations
- **Persistent Storage**: SQLite database with Docker volume mapping
- **Responsive UI**: Built with Radzen Blazor components

## Tech Stack

- **Backend**: ASP.NET Core Web API (.NET 10)
- **Frontend**: Blazor Server (.NET 10)
- **Authentication**: OpenID Connect + JWT Bearer
- **UI Library**: Radzen.Blazor (Free/Community components)
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

# Edit the docker-compose.yml to use local builds instead of GHCR images:
# Replace 'image: ghcr.io/...' with:
#   build:
#     context: .
#     dockerfile: Dockerfile.api  # (or Dockerfile for web)

docker compose up -d --build
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

3. **Access**: `https://localhost:5001` or `http://localhost:5000`

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
  - Allowed Callback URLs: `https://localhost:5001/signin-oidc`
  - Allowed Logout URLs: `https://localhost:5001/`
  - Allowed Web Origins: `https://localhost:5001`
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
2. Open `https://localhost:5001`
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

- `http://localhost:5003/swagger` (local development)
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
│   │   ├── Controllers/
│   │   │   └── TripsController.cs       # REST endpoints
│   │   ├── Services/
│   │   │   └── TripService.cs           # Business logic
│   │   ├── Data/
│   │   │   └── ApplicationDbContext.cs  # EF Core DbContext
│   │   ├── Models/
│   │   │   └── Trip.cs                  # Entity model
│   │   └── Program.cs                   # API configuration
│   │
│   ├── ResidencyRoll.Web/               # Blazor Frontend
│   │   ├── Components/
│   │   │   ├── Pages/
│   │   │   │   ├── Home.razor           # Dashboard
│   │   │   │   ├── ManageTrips.razor    # CRUD interface
│   │   │   │   └── Forecast.razor       # Trip planning
│   │   │   └── LoginDisplay.razor       # Auth UI
│   │   ├── Services/
│   │   │   ├── TripsApiClient.cs        # API client
│   │   │   └── ApiAuthenticationHandler.cs
│   │   └── Program.cs                   # Web app configuration
│   │
│   └── ResidencyRoll.Shared/            # Shared DTOs
│       └── Trips/
│           └── TripDto.cs               # Data transfer objects
│
├── tests/
│   └── ResidencyRoll.Tests/             # Unit tests
│
├── Dockerfile                           # Web container
├── Dockerfile.api                       # API container
├── docker-compose.yml                   # Multi-container orchestration
└── README.md                            # This file
```

## How It Works

### Rolling 365-Day Calculation

The application implements smart overlap detection:

- If a trip started 370 days ago and ended 350 days ago, only the 15 days within the 365-day window are counted
- The calculation is relative to "Today" and updates automatically
- Days are counted inclusively (both start and end dates included)

### Data Management

- **Add Trip**: Click "Add Trip" button in the data grid
- **Edit Trip**: Click the edit icon on any row
- **Delete Trip**: Click the delete icon on any row
- All changes are immediately persisted to the SQLite database

### Docker Volume

The application uses a named Docker volume (`residencyroll-data`) stored in `/var/lib/docker/volumes/` to ensure your SQLite database persists across container restarts.

## Deployment

### GitHub Container Registry (GHCR)

The project uses GitHub Actions to automatically build and push Docker images to GHCR on every push to `main`.

**Using pre-built images:**

```yaml
services:
  residencyroll-api:
    image: ghcr.io/glenconway/residencyroll-api:latest
    # ... configuration
  
  residencyroll-web:
    image: ghcr.io/glenconway/residencyroll-web:latest
    # ... configuration
```

### Database Backup & Restore

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

### Environment Variables

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
| ------- | ---------- |
| 401 Unauthorized from API | Check JWT Authority and Audience match between API and identity provider |
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
