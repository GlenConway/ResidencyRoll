# ResidencyRoll - Quick Start Guide

## Option 1: Docker (Recommended)

The fastest way to run the application with persistent data.

```bash
# From the ResidencyRoll directory
docker-compose up -d

# View logs
docker-compose logs -f

# Stop the application
docker-compose down
```

Access at: `http://localhost:8080`

The SQLite database will be stored in the `./db` folder and persists across restarts.

## Option 2: Local Development

```bash
# Navigate to the web project
cd src/ResidencyRoll.Web

# Run the application
dotnet run

# Or with hot reload
dotnet watch run
```

Access at: `https://localhost:5001` or `http://localhost:5000`

## First Time Setup

1. **Add your first trip:**
   - Click the "Add Trip" button in the data grid
   - Enter country name, start date, and end date
   - Click the checkmark to save

2. **View your statistics:**
   - Arc Gauge shows Days Away vs Days Home for the last 365 days
   - Donut Chart displays distribution across countries
   - Timeline shows chronological trip history

3. **Manage existing trips:**
   - Click the edit icon to modify a trip
   - Click the delete icon to remove a trip

## Understanding the Rolling 365-Day Calculation

The application only counts days within the last 365 days from today:

- **Example 1:** Trip from 370 days ago to 350 days ago
  - Only 15 days are counted (the overlap with the 365-day window)

- **Example 2:** Trip from 30 days ago to 10 days ago
  - All 21 days are counted

- **Example 3:** Trip from 30 days ago to 30 days in the future
  - Only 31 days are counted (30 days ago to today)

The calculation automatically updates each day!

## Troubleshooting

### Docker Issues

```bash
# Rebuild the container
docker-compose up --build

# View container logs
docker-compose logs -f

# Clean up everything and start fresh
docker-compose down -v
docker-compose up --build
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

### Database Location

- **Docker:** `./db/residencyroll.db`
- **Local:** `/app/data/residencyroll.db` (created automatically)

## Next Steps

- Customize the theme by changing the Radzen CSS in `App.razor`
- Add data validation to the Trip model
- Export trip data to CSV or Excel
- Add authentication if deploying to production
- Set up automated backups of the SQLite database

Enjoy tracking your travels! 🌍✈️
