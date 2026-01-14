# Application Versioning

## Overview

Both the API and Web projects now support versioning that can be set during build time. The version is logged on application startup for diagnostic purposes to ensure you're running the expected container version.

## Version Configuration

### Default Version
- Default version is set to `1.0.0` in the `.csproj` files
- This version is used when no explicit version is provided during build

### Setting Version at Build Time

#### Local Development
```bash
# Build with specific version
dotnet build -p:Version=1.2.3

# Publish with specific version
dotnet publish -p:Version=1.2.3
```

#### Docker Build
```bash
# Build Web container with version
docker build -f Dockerfile --build-arg VERSION=1.2.3 -t residencyroll-web:1.2.3 .

# Build API container with version
docker build -f Dockerfile.api --build-arg VERSION=1.2.3 -t residencyroll-api:1.2.3 .
```

#### Docker Compose
Update your `docker-compose.yml` to include build args:
```yaml
services:
  api:
    build:
      context: .
      dockerfile: Dockerfile.api
      args:
        VERSION: ${APP_VERSION:-1.0.0}
    # ... rest of config

  web:
    build:
      context: .
      dockerfile: Dockerfile
      args:
        VERSION: ${APP_VERSION:-1.0.0}
    # ... rest of config
```

Then build with:
```bash
APP_VERSION=1.2.3 docker-compose build
```

### GitHub Actions Workflow

In your GitHub Actions workflow, you can use the run number or create a version from tags:

```yaml
name: Build and Deploy

on:
  push:
    branches: [ main, dev ]
  
env:
  # Create version from run number: 1.0.BUILD_NUMBER
  VERSION: 1.0.${{ github.run_number }}

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Build API Docker image
        run: |
          docker build -f Dockerfile.api \
            --build-arg VERSION=${{ env.VERSION }} \
            -t your-registry/residencyroll-api:${{ env.VERSION }} \
            .
      
      - name: Build Web Docker image
        run: |
          docker build -f Dockerfile \
            --build-arg VERSION=${{ env.VERSION }} \
            -t your-registry/residencyroll-web:${{ env.VERSION }} \
            .
```

#### Using Git Tags for Versioning

For semantic versioning based on git tags:

```yaml
name: Build and Deploy

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Get version from tag
        id: version
        run: echo "VERSION=${GITHUB_REF#refs/tags/v}" >> $GITHUB_OUTPUT
      
      - name: Build API Docker image
        run: |
          docker build -f Dockerfile.api \
            --build-arg VERSION=${{ steps.version.outputs.VERSION }} \
            -t your-registry/residencyroll-api:${{ steps.version.outputs.VERSION }} \
            .
      
      - name: Build Web Docker image
        run: |
          docker build -f Dockerfile \
            --build-arg VERSION=${{ steps.version.outputs.VERSION }} \
            -t your-registry/residencyroll-web:${{ steps.version.outputs.VERSION }} \
            .
```

## Viewing Version in Logs

When the application starts, the version will be logged:

### API Logs
```
[INF] Starting ResidencyRoll API - Version: 1.2.3
```

### Web Logs
```
[INF] Starting ResidencyRoll Web - Version: 1.2.3
```

### Checking Container Logs

```bash
# Check API container logs
docker logs residencyroll-api 2>&1 | grep "Version:"

# Check Web container logs
docker logs residencyroll-web 2>&1 | grep "Version:"
```

## Version Properties

The following .NET assembly attributes are set by the version:
- `Version`: The version number (e.g., 1.2.3)
- `AssemblyVersion`: Used for strong naming and .NET assembly versioning
- `FileVersion`: File version visible in file properties
- `InformationalVersion`: Full version string that can include additional metadata (like git commit hash)

## Best Practices

1. **Use semantic versioning**: MAJOR.MINOR.PATCH (e.g., 1.2.3)
2. **Increment versions appropriately**:
   - MAJOR: Breaking changes
   - MINOR: New features (backward compatible)
   - PATCH: Bug fixes
3. **Include build metadata** in CI/CD:
   - Build number: `1.2.3-build.456`
   - Git commit: `1.2.3+sha.abc1234`
4. **Tag releases** in Git to track deployed versions
5. **Always check logs** after deployment to verify the version

## Troubleshooting

### Version shows as "unknown"
- Ensure the `Version` property is set in the `.csproj` file
- Verify the build argument is passed correctly during Docker build
- Check that the application was rebuilt after changing the version

### Version not updating in containers
- Rebuild the Docker image with `--no-cache` flag
- Ensure you're not using cached layers
- Verify the build arg is being passed: `docker build --build-arg VERSION=x.y.z ...`
