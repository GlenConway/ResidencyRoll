# Deployment Guide - GitHub Container Registry (GHCR)

## Automated Deployment (Recommended)

The GitHub Actions workflow (`.github/workflows/docker-publish.yml`) automatically builds and pushes to GHCR when you:

1. **Push to main branch**: Creates `ghcr.io/glenconway/residencyroll:latest` and `ghcr.io/glenconway/residencyroll:main`
2. **Create a version tag**: Creates versioned images (e.g., `v1.0.0` → `ghcr.io/glenconway/residencyroll:1.0.0`)
3. **Manual trigger**: Use the "Actions" tab → "Build and Push to GHCR" → "Run workflow"

### Setup Steps

1. Commit and push the workflow file:
```bash
git add .github/workflows/docker-publish.yml
git commit -m "Add GitHub Actions workflow for GHCR deployment"
git push origin main
```

2. The workflow will automatically run and push the image to GHCR

3. Make the package public (optional):
   - Go to https://github.com/GlenConway?tab=packages
   - Click on `residencyroll` package
   - Click "Package settings"
   - Scroll down to "Danger Zone" → "Change package visibility"
   - Select "Public"

## Manual Deployment

If you prefer to build and push manually:

```bash
# 1. Login to GHCR (one-time setup)
echo $GITHUB_TOKEN | docker login ghcr.io -u GlenConway --password-stdin

# 2. Build the image
docker build -t ghcr.io/glenconway/residencyroll:latest .

# 3. Push to GHCR
docker push ghcr.io/glenconway/residencyroll:latest
```

**Note**: You'll need a GitHub Personal Access Token (PAT) with `write:packages` scope. Create one at: https://github.com/settings/tokens

## Running from GHCR

### Using docker compose

Create a `docker compose.yml` on your target machine:

```yaml
services:
  residencyroll:
    image: ghcr.io/glenconway/residencyroll:latest
    container_name: residencyroll-app
    ports:
      - "8753:8080"  # Maps external port 8753 to internal container port 8080
    volumes:
      - residencyroll-data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
    restart: unless-stopped

volumes:
  residencyroll-data:
```

Then run:
```bash
docker compose up -d
```

### Using docker run

```bash
docker run -d \
  --name residencyroll-app \
  -p 8753:8080 \
  -v residencyroll-data:/app/data \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:8080 \
  --restart unless-stopped \
  ghcr.io/glenconway/residencyroll:latest
```

**Note**: This creates a Docker-managed volume at `/var/lib/docker/volumes/residencyroll-data`.

## Image Tags

The workflow creates multiple tags:

- `latest` - Always points to the most recent main branch build
- `main` - Latest build from main branch
- `v1.0.0` - Specific version (when you create git tags)
- `1.0` - Major.minor version
- `1` - Major version only
- `main-abc1234` - Branch name + commit SHA

## Updating Your Deployment

To pull the latest image:

```bash
docker compose pull
docker compose up -d
```

Or with docker run:

```bash
docker pull ghcr.io/glenconway/residencyroll:latest
docker stop residencyroll-app
docker rm residencyroll-app
# Then run the docker run command again
```

## Creating Version Releases

To create a versioned release:

```bash
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

This will trigger the workflow to build and push with version tags.
