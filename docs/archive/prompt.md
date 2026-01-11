# Project Goal: ResidencyRoll
Build a travel tracking web application named "ResidencyRoll" using ASP.NET Core Blazor (Server-side) on .NET 10. The app tracks international trips to calculate days spent in each country over a rolling 12-month (365-day) period for residency and tax purposes.

# Tech Stack & Constraints
- Framework: Blazor Server (.NET 10)
- UI Library: Radzen.Blazor (Free/Community components)
- Persistence: SQLite with EF Core (No external DB licenses)
- Deployment: Dockerized with persistent volume mapping

# Core Requirements

1. Data Model:
   - Trip: Id (int), CountryName (string), StartDate (DateTime), EndDate (DateTime).

2. Rolling 12-Month Logic:
   - Implement a service that calculates days spent in each country within the last 365 days relative to "Today."
   - Logic must handle overlaps: If a trip started 370 days ago and ended 350 days ago, only the 15 days within the 365-day window should be counted.

3. Radzen UI Components:
   - Main Dashboard: Use RadzenArcGauge to show "Days Away" vs "Days at Home" for the current year.
   - Trip Management: A RadzenDataGrid with Inline CRUD (Add/Edit/Delete) functionality.
   - Visualization: A RadzenChart (Donut) showing the distribution of days spent per country.
   - History: A RadzenTimeline to visualize the chronological trip history.

4. Data Persistence & Docker:
   - Store the SQLite database file in a folder named `/app/data`.
   - Provide a Dockerfile using the .NET 10 SDK/Runtime.
   - Provide a docker compose.yml that includes a volume mapping (e.g., `./db:/app/data`) to ensure the SQLite file persists across container restarts.

5. Code Structure:
   - Initialize Radzen services in Program.cs.
   - Create a 'TripService' for the rolling window calculations.
   - Provide the Razor page code for the dashboard and the trip entry grid.

# Output Instructions
Please generate the project structure, the Trip model, the EF Core DbContext, the Calculation Service, the Blazor UI code (Index.razor), and the Docker configuration files.
