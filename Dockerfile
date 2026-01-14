# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=1.0.0
WORKDIR /src

# Copy shared project
COPY src/ResidencyRoll.Shared/ResidencyRoll.Shared.csproj ./ResidencyRoll.Shared/
RUN dotnet restore ./ResidencyRoll.Shared/ResidencyRoll.Shared.csproj

# Copy project file and restore dependencies
COPY src/ResidencyRoll.Web/ResidencyRoll.Web.csproj ./ResidencyRoll.Web/
RUN dotnet restore ./ResidencyRoll.Web/ResidencyRoll.Web.csproj

# Copy everything else and build
COPY src/ResidencyRoll.Shared/ ./ResidencyRoll.Shared/
COPY src/ResidencyRoll.Web/ ./ResidencyRoll.Web/
WORKDIR /src/ResidencyRoll.Web
RUN dotnet build -c Release -o /app/build -p:Version=${VERSION}

# Publish stage
FROM build AS publish
ARG VERSION=1.0.0
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false -p:Version=${VERSION}

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create data directory for SQLite database
RUN mkdir -p /app/data

COPY --from=publish /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ResidencyRoll.Web.dll"]
