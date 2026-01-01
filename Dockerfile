# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY src/ResidencyRoll.Web/ResidencyRoll.Web.csproj ./ResidencyRoll.Web/
RUN dotnet restore ./ResidencyRoll.Web/ResidencyRoll.Web.csproj

# Copy everything else and build
COPY src/ResidencyRoll.Web/ ./ResidencyRoll.Web/
WORKDIR /src/ResidencyRoll.Web
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create data directory for SQLite database
RUN mkdir -p /app/data

COPY --from=publish /app/publish .

# Expose port
EXPOSE 8753

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8753
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ResidencyRoll.Web.dll"]
