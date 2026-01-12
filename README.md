# ResidencyRoll

[![Tests](https://github.com/GlenConway/ResidencyRoll/actions/workflows/tests.yml/badge.svg?branch=main)](https://github.com/GlenConway/ResidencyRoll/actions/workflows/tests.yml)
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

#### Using Pre-built Image (Production)

```bash
# Download the docker compose file
curl -O https://raw.githubusercontent.com/GlenConway/ResidencyRoll/main/docker-compose.yml

# Start the services
docker compose up -d
```

Access the application:

- **Web UI**: `http://localhost:8081`
- **API**: `http://localhost:8080`
- **API Swagger**: `http://localhost:8080/swagger`

#### Building from Source

```bash
# Clone and build
git clone https://github.com/GlenConway/ResidencyRoll.git
cd ResidencyRoll
docker compose up -d --build
```

The SQLite database will be persisted in Docker volumes (`residencyroll-api-data` and `residencyroll-web-data`).

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

1. **Create `.env` file** (see `.env.example`):

   ```bash
   # Identity Provider Configuration
   OIDC_ENABLED=true
   OIDC_AUTHORITY=https://your-identity-provider.com
   OIDC_CLIENT_ID=residencyroll-web
   OIDC_CLIENT_SECRET=your-secret-here
   OIDC_REQUIRE_HTTPS=true
   OIDC_API_SCOPE=residencyroll-api
   
   # API JWT Configuration
   JWT_AUTHORITY=https://your-identity-provider.com
   JWT_AUDIENCE=residencyroll-api
   JWT_REQUIRE_HTTPS=true
   ```

2. **Deploy**:

   ```bash
   docker compose up -d
   ```

3. **Security Checklist**:
   - [ ] Set `RequireHttpsMetadata: true`
   - [ ] Use HTTPS for all endpoints
   - [ ] Store secrets in secure vault (Azure Key Vault, AWS Secrets, etc.)
   - [ ] Configure proper CORS origins (no wildcards)
   - [ ] Set appropriate token expiration times
   - [ ] Enable security headers (HSTS, CSP, etc.)

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
