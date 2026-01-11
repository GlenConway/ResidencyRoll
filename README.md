# ResidencyRoll

[![Tests](https://github.com/GlenConway/ResidencyRoll/actions/workflows/tests.yml/badge.svg?branch=main)](https://github.com/GlenConway/ResidencyRoll/actions/workflows/tests.yml)
[![License](https://img.shields.io/github/license/GlenConway/ResidencyRoll.svg)](LICENSE)

A travel tracking web application built with ASP.NET Core that helps you track international trips and calculate days spent in each country over a rolling 12-month (365-day) period for residency and tax purposes.

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

## Getting Started

### Running with Docker (Recommended)

#### Option 1: Using Pre-built Image (Production)

1. Download the docker compose file:
```bash
curl -O https://raw.githubusercontent.com/GlenConway/ResidencyRoll/main/docker-compose.yml
```

2. Start the container:
```bash
docker compose up -d
```

3. Access the application at: `http://localhost:8753`

The SQLite database will be persisted in a Docker-managed volume at `/var/lib/docker/volumes/residencyroll-data`.

#### Option 2: Building Locally

1. Clone the repository and build:
```bash
docker compose up -d --build
```

2. Access the application at: `http://localhost:8753`

### Running Locally (Development)

**Without Authentication (Quick Start):**

1. Start the API:
```bash
cd src/ResidencyRoll.Api
dotnet watch run
```

2. In a new terminal, start the Web app:
```bash
cd src/ResidencyRoll.Web
dotnet watch run
```

3. Access the application at: `https://localhost:5001` (or the port shown in console)

**With Authentication:**
├── ResidencyRoll.Api/               # API Backend
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
├── AUTHENTICATION.md                    # Auth setup guide
├── AUTH_QUICK_REFERENCE.md             # Quick auth reference
Authentication is **disabled by default** for local development. See [AUTHENTICATION.md](AUTHENTICATION.md) for production setup.

## API Documentation

When running the API in development mode, Swagger UI is available at:
- `http://localhost:5003/swagger` (API running locally)
- `http://localhost:8080/swagger` (API in Docker)

The API supports versioning and includes comprehensive OpenAPI documentation.

## Project Structure

```
ResidencyRoll/
├── src/
│   └── ResidencyRoll.Web/
│       ├── Components/
│       │   ├── Pages/
│       │   │   └── Home.razor          # Main dashboard
│       │   └── App.razor
│       ├── Data/
│       │   └── ApplicationDbContext.cs  # EF Core DbContext
│       ├── Models/
│       │   └── Trip.cs                  # Trip entity
│       ├── Services/
│       │   └── TripService.cs           # Rolling calculation logic
│       └── Program.cs
├── Dockerfile
├── docker compose.yml
└── README.md
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

## Development Notes

- The application uses Blazor Server with interactive rendering mode
- Database is automatically created on first run using `EnsureCreated()`
- Radzen components use the Material theme by default

## License

MIT
