# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

ResidencyRoll is a Blazor Server web application (.NET 10) that tracks international travel and calculates days spent in each country over a rolling 365-day period for residency and tax purposes. It uses SQLite for persistence and Radzen Blazor components for the UI.

## Development Commands

### Build and Run
```bash
# Build the project
dotnet build src/ResidencyRoll.Web/ResidencyRoll.Web.csproj

# Run locally (Development mode)
cd src/ResidencyRoll.Web
dotnet run

# Watch mode for hot-reload during development
dotnet watch run --project src/ResidencyRoll.Web/ResidencyRoll.Web.csproj
```

### Docker
```bash
# Build and run with Docker (recommended for production testing)
docker compose up -d

# Stop the container
docker compose down

# View logs
docker logs residencyroll-app

# Rebuild after code changes
docker compose up -d --build
```

### Testing
Currently, there are no test projects in the solution. When adding tests, they should:
- Use xUnit, Shouldly, and Moq (per user test rules)
- Follow the naming convention: `MethodName_Condition_ShouldExpectedBehavior`
- Be placed in a separate test project following the structure: `tests/ResidencyRoll.Web.Tests/`

## Architecture

### Core Components

**Models** (`Models/Trip.cs`)
- `Trip` entity with Id, CountryName, StartDate, EndDate
- Computed property `DurationDays` calculates inclusive duration

**Data Layer** (`Data/ApplicationDbContext.cs`)
- Entity Framework Core with SQLite
- Single `Trips` DbSet
- Database auto-created on startup using `EnsureCreated()`
- Database location: `./data/residencyroll.db` (dev) or `/app/data/residencyroll.db` (Docker)

**Services** (`Services/TripService.cs`)
- `TripService` handles all business logic
- Key method: `GetDaysPerCountryInLast365DaysAsync()` implements rolling 365-day window calculation with overlap detection
- Overlap logic: If a trip extends outside the 365-day window, only days within the window are counted

**Pages** (`Components/Pages/`)
- `Home.razor`: Dashboard with charts (bar chart for days distribution, donut chart for country breakdown) and timeline
- `ManageTrips.razor`: CRUD interface using Radzen DataGrid with inline editing, CSV import/export
- Both use `@rendermode InteractiveServer` for Blazor Server interactivity

**API Endpoints** (`Program.cs`)
- `GET /api/trips/export`: Exports trips as CSV
- `POST /api/trips/import`: Imports trips from CSV file

### Key Patterns

**Rolling 365-Day Calculation**
The calculation is relative to "today" and handles trip overlaps intelligently:
- Window: `DateTime.Today.AddDays(-365)` to `DateTime.Today`
- For each trip, calculate overlap between trip dates and window
- Days are counted inclusively (both start and end dates included)
- Example: A trip from 370 days ago to 350 days ago contributes only 15 days (the portion within the window)

**Data Persistence**
- SQLite database with volume mapping in Docker (`./db:/app/data`)
- No migrations; database schema is created automatically on first run
- All CRUD operations go through `TripService` for consistency

**UI Framework**
- Radzen Blazor (v8.4.2) provides all UI components
- Material theme applied globally
- Components: `RadzenDataGrid`, `RadzenChart` (Bar/Donut), `RadzenTimeline`, `RadzenCard`, etc.

## Development Workflow

1. **Making changes**: Edit files in `src/ResidencyRoll.Web/`
2. **Testing locally**: Use `dotnet watch run` for hot-reload
3. **Database**: Located in `./data/` directory (gitignored), delete to reset
4. **Deployment**: Push to `main` branch triggers GitHub Actions to build and push Docker image to GHCR
5. **Versioned releases**: Create git tags (`v1.0.0`) to trigger versioned Docker images

## Important Notes

- Target framework: .NET 10.0
- This is a single-project solution (no separate class libraries)
- No authentication is currently implemented (per user authentication rule to use existing mechanisms)
- Database schema changes require manual migration or database recreation
- Radzen components are from the free/community edition
- The application uses Blazor Server (not WebAssembly), maintaining persistent connection via SignalR

## File Structure
```
src/ResidencyRoll.Web/
├── Components/
│   ├── Pages/          # Razor pages (Home, ManageTrips, Error, NotFound)
│   ├── Layout/         # Layout components (MainLayout, NavMenu, ReconnectModal)
│   └── App.razor       # Root component
├── Data/               # EF Core DbContext
├── Models/             # Entity models
├── Services/           # Business logic layer
└── Program.cs          # Application entry point and API endpoints
```
