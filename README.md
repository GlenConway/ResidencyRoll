# ResidencyRoll

A travel tracking web application built with ASP.NET Core Blazor Server (.NET 10) that helps you track international trips and calculate days spent in each country over a rolling 12-month (365-day) period for residency and tax purposes.

## Features

- **Rolling 365-Day Calculations**: Automatically calculates days spent in each country within the last 365 days from today
- **Visual Dashboard**: 
  - Arc Gauge showing Days Away vs Days at Home
  - Donut Chart displaying country distribution
  - Interactive Timeline of all trips
- **Trip Management**: Inline CRUD operations using Radzen DataGrid
- **Persistent Storage**: SQLite database with Docker volume mapping
- **Responsive UI**: Built with Radzen Blazor components

## Tech Stack

- **Framework**: Blazor Server (.NET 10)
- **UI Library**: Radzen.Blazor (Free/Community components)
- **Database**: SQLite with Entity Framework Core
- **Deployment**: Docker with persistent volume

## Getting Started

### Running with Docker (Recommended)

#### Option 1: Using Pre-built Image (Production)

1. Download the docker-compose file:
```bash
curl -O https://raw.githubusercontent.com/GlenConway/ResidencyRoll/main/docker-compose.yml
```

2. Start the container:
```bash
docker-compose up -d
```

3. Access the application at: `http://localhost:8753`

The SQLite database will be persisted in a Docker-managed volume at `/var/lib/docker/volumes/residencyroll-data`.

#### Option 2: Building Locally

1. Clone the repository and build:
```bash
docker-compose up -d --build
```

2. Access the application at: `http://localhost:8753`

### Running Locally (Development)

1. Navigate to the project directory:
```bash
cd src/ResidencyRoll.Web
```

2. Run the application:
```bash
dotnet run
```

3. Access the application at: `https://localhost:5001` (or the port shown in the console)

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
├── docker-compose.yml
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
