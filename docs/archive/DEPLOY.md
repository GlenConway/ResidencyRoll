# Deployment Guide

## Architecture Overview

ResidencyRoll now uses a split architecture:
- **API Service** (`Dockerfile.api`): Backend REST API with business logic and database
- **Web Service** (`Dockerfile`): Blazor frontend that consumes the API

Both services run in separate containers and communicate over a shared Docker network.

## Quick Start - Local Development

### Using Docker Compose (Recommended)

1. Build and start both services:
   ```bash
   docker compose up --build
   ```

2. Access the applications:
   - Web UI: http://localhost:8081
   - API: http://localhost:8080
   - API Swagger: http://localhost:8080/swagger

3. Stop the services:
   ```bash
   docker compose down
   ```

### Build Individual Images

**API:**
```bash
docker build -f Dockerfile.api -t residencyroll-api:latest .
```

**Web:**
```bash
docker build -f Dockerfile -t residencyroll-web:latest .
```

## Production Deployment

### Using Docker Compose with Pre-built Images

Create a `docker-compose.yml` on your target machine:

```yaml
services:
  residencyroll-api:
    image: ghcr.io/glenconway/residencyroll-api:latest
    container_name: ResidencyRoll-Api
    ports:
      - "8080:80"
    volumes:
      - residencyroll-api-data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:80
      - ConnectionStrings__Default=Data Source=/app/data/residencyroll.db
      - Jwt__Authority=${JWT_AUTHORITY}
      - Jwt__Audience=${JWT_AUDIENCE}
      - Cors__AllowedOrigins__0=http://localhost:8081
    restart: unless-stopped
    networks:
      - residencyroll-network

  residencyroll-web:
    image: ghcr.io/glenconway/residencyroll-web:latest
    container_name: ResidencyRoll-Web
    ports:
      - "8081:8080"
    volumes:
      - residencyroll-web-data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - Api__BaseUrl=http://residencyroll-api:80
    restart: unless-stopped
    depends_on:
      - residencyroll-api
    networks:
      - residencyroll-network

volumes:
  residencyroll-api-data:
  residencyroll-web-data:

networks:
  residencyroll-network:
    driver: bridge
```

Then run:
```bash
docker compose up -d
```

### Environment Configuration

Create a `.env` file for sensitive configuration:

```bash
JWT_AUTHORITY=https://your-identity-provider.com/
JWT_AUDIENCE=residencyroll-api
JWT_REQUIRE_HTTPS=true
```

See `.env.example` for all available options.

## GitHub Container Registry (GHCR) Setup

### Automated CI/CD

Update `.github/workflows/docker-publish.yml` to build both images:

```yaml
jobs:
  build-api:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Build and push API
        run: |
          docker build -f Dockerfile.api -t ghcr.io/glenconway/residencyroll-api:latest .
          docker push ghcr.io/glenconway/residencyroll-api:latest
  
  build-web:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Build and push Web
        run: |
          docker build -f Dockerfile -t ghcr.io/glenconway/residencyroll-web:latest .
          docker push ghcr.io/glenconway/residencyroll-web:latest
```

### Manual Push to GHCR

```bash
# Login to GHCR
echo $GITHUB_TOKEN | docker login ghcr.io -u GlenConway --password-stdin

# Build and push API
docker build -f Dockerfile.api -t ghcr.io/glenconway/residencyroll-api:latest .
docker push ghcr.io/glenconway/residencyroll-api:latest

# Build and push Web
docker build -f Dockerfile -t ghcr.io/glenconway/residencyroll-web:latest .
docker push ghcr.io/glenconway/residencyroll-web:latest
```

## Database Persistence

SQLite databases are stored in Docker volumes:

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

## Authentication Setup

Before enabling JWT auth in production:

1. Configure OIDC provider (Auth0, Keycloak, Azure AD)
2. Set `JWT_AUTHORITY` and `JWT_AUDIENCE` environment variables
3. Configure Blazor OIDC client
4. Re-enable `[Authorize]` attribute in API controllers
5. Test token acquisition and API calls

## Troubleshooting

**View logs:**
```bash
docker compose logs -f
docker compose logs -f residencyroll-api
docker compose logs -f residencyroll-web
```

**Check container status:**
```bash
docker compose ps
```

**Access container shell:**
```bash
docker exec -it ResidencyRoll-Api /bin/bash
```

**Reset and rebuild:**
```bash
docker compose down -v
docker compose up --build
```

## Legacy Single-Container Deployment

For the legacy monolith deployment (ResidencyRoll.Web only), see git history before the API split.
