# Configuration Setup

## First Time Setup

Before running the application, you need to set up your development configuration files:

### Quick Setup

```bash
# Copy example files to actual configuration files
cp src/ResidencyRoll.Web/appsettings.Development.json.example src/ResidencyRoll.Web/appsettings.Development.json
cp src/ResidencyRoll.Api/appsettings.Development.json.example src/ResidencyRoll.Api/appsettings.Development.json
```

### Configure Authentication

You have two options for configuring your Auth0 credentials:

#### Option 1: User Secrets (Recommended - Most Secure)

Run the automated configuration script:
```bash
./configure-auth0.sh
```

This will store your credentials securely in .NET User Secrets, keeping them completely out of your source tree.

#### Option 2: Edit Configuration Files Directly

After copying the example files above, edit them to add your Auth0 credentials:

**Web:** `src/ResidencyRoll.Web/appsettings.Development.json`
```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Authority": "https://your-tenant.auth0.com/",
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret"
    }
  }
}
```

**API:** `src/ResidencyRoll.Api/appsettings.Development.json`
```json
{
  "Jwt": {
    "Authority": "https://your-tenant.auth0.com/",
    "Audience": "your-api-identifier"
  }
}
```

⚠️ **Important:** These files are in `.gitignore` and will NOT be committed to source control.

## What's Ignored by Git

The following files are excluded from version control to protect your secrets:

- `appsettings.Development.json` (both Web and API)
- Any `appsettings.*.json` files (e.g., appsettings.Staging.json)

The following files ARE committed:
- `appsettings.json` (base configuration, no secrets)
- `*.example.json` files (templates with placeholders)

## For New Team Members

1. Clone the repository
2. Copy the `.example.json` files as shown above
3. Get Auth0 credentials from your team lead
4. Either run `./configure-auth0.sh` or edit the files directly
5. Run the application

## Verifying Your Setup

After configuration, verify your setup:

```bash
# Check that configuration files exist
ls -la src/ResidencyRoll.Web/appsettings.Development.json
ls -la src/ResidencyRoll.Api/appsettings.Development.json

# Verify they're not tracked by git
git status --ignored | grep appsettings
```

You should see them listed as ignored files.

## Documentation

For complete Auth0 setup instructions, see:
- [AUTH0_SETUP.md](AUTH0_SETUP.md) - Detailed Auth0 configuration guide
- [AUTH0_VALUES_GUIDE.md](AUTH0_VALUES_GUIDE.md) - Visual guide for finding Auth0 values
