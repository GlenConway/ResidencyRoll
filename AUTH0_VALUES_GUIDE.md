# Where to Find Your Auth0 Configuration Values

## Quick Reference Table

| Configuration Value | Location in Auth0 Dashboard | Example Value | Used In |
|-------------------|---------------------------|---------------|---------|
| **Authority** | Applications → Applications → Your App → Domain | `https://dev-abc123.auth0.com/` | Web & API |
| **ClientId** | Applications → Applications → Your App → Client ID | `aBcD1234567890XyZ` | Web only |
| **ClientSecret** | Applications → Applications → Your App → Client Secret | `ABC123xyz...` | Web only |
| **Audience** | Applications → APIs → Your API → Identifier | `https://api.residencyroll.com` | API only |

---

## Step-by-Step: Finding Your Values

### 1. Authority (Both Web & API)

```
Auth0 Dashboard
└── Applications
    └── Applications
        └── ResidencyRoll Web
            └── Settings tab
                └── Domain: "dev-abc123.auth0.com"
```

**Your Authority value is:**
- Add `https://` at the beginning
- Add `/` at the end
- Result: `https://dev-abc123.auth0.com/`

---

### 2. ClientId (Web only)

```
Auth0 Dashboard
└── Applications
    └── Applications
        └── ResidencyRoll Web
            └── Settings tab
                └── Client ID: "aBcD1234567890XyZ"
```

**Copy this value exactly as shown**

---

### 3. ClientSecret (Web only)

```
Auth0 Dashboard
└── Applications
    └── Applications
        └── ResidencyRoll Web
            └── Settings tab
                └── Client Secret: [Click to reveal] → "ABC123xyz..."
```

**Important:**
- Click "Show" or the eye icon to reveal the secret
- Copy it immediately (you may not be able to see it again)
- **Never commit this to source control!**

---

### 4. Audience (API only)

```
Auth0 Dashboard
└── Applications
    └── APIs
        └── ResidencyRoll API
            └── Identifier: "https://api.residencyroll.com"
```

**This can be any unique identifier:**
- Common format: `https://api.yourapp.com`
- Or a simple identifier: `residencyroll-api`
- Use the exact value you entered when creating the API

---

## Where to Set These Values

### Method 1: User Secrets (Recommended - Secure)

Run the configuration script:
```bash
./configure-auth0.sh
```

Or manually:
```bash
# Web App
cd src/ResidencyRoll.Web
dotnet user-secrets set "Authentication:OpenIdConnect:Authority" "https://YOUR-TENANT.auth0.com/"
dotnet user-secrets set "Authentication:OpenIdConnect:ClientId" "YOUR-CLIENT-ID"
dotnet user-secrets set "Authentication:OpenIdConnect:ClientSecret" "YOUR-CLIENT-SECRET"

# API
cd ../ResidencyRoll.Api
dotnet user-secrets set "Jwt:Authority" "https://YOUR-TENANT.auth0.com/"
dotnet user-secrets set "Jwt:Audience" "YOUR-API-IDENTIFIER"
```

### Method 2: Environment Variables

```bash
# Web App
export Authentication__OpenIdConnect__Authority="https://YOUR-TENANT.auth0.com/"
export Authentication__OpenIdConnect__ClientId="YOUR-CLIENT-ID"
export Authentication__OpenIdConnect__ClientSecret="YOUR-CLIENT-SECRET"

# API
export Jwt__Authority="https://YOUR-TENANT.auth0.com/"
export Jwt__Audience="YOUR-API-IDENTIFIER"
```

### Method 3: Configuration Files (⚠️ Not Recommended)

You can update these files directly, but **DO NOT COMMIT SECRETS**:
- `src/ResidencyRoll.Web/appsettings.Development.json`
- `src/ResidencyRoll.Api/appsettings.Development.json`

Replace the placeholder values:
- `YOUR-TENANT` → Your actual tenant name
- `YOUR-AUTH0-CLIENT-ID` → Your Client ID
- `YOUR-AUTH0-CLIENT-SECRET` → Your Client Secret
- `YOUR-AUTH0-API-IDENTIFIER` → Your API Identifier

**⚠️ Important:** If you use this method, run `git restore` on these files before committing!

---

## Verify Your Configuration

### Check User Secrets:

```bash
cd src/ResidencyRoll.Web
dotnet user-secrets list

cd ../ResidencyRoll.Api
dotnet user-secrets list
```

### Test the Configuration:

1. Start both applications:
   ```bash
   # Terminal 1
   cd src/ResidencyRoll.Api
   dotnet watch run
   
   # Terminal 2
   cd src/ResidencyRoll.Web
   dotnet watch run
   ```

2. Open browser to Web app (typically `https://localhost:5001`)

3. Click the **Login** button

4. You should be redirected to Auth0 login page

5. If it works, you'll see the Auth0 login form with your tenant name

---

## Example: Complete Configuration

Here's what a complete set of values looks like:

**From Auth0:**
- Domain: `dev-abc123.auth0.com`
- Client ID: `aBcD1234567890XyZ`
- Client Secret: `verySecretString123_abc`
- API Identifier: `https://api.residencyroll.com`

**In your configuration:**

Web (`appsettings.Development.json` or user secrets):
```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "Authority": "https://dev-abc123.auth0.com/",
      "ClientId": "aBcD1234567890XyZ",
      "ClientSecret": "verySecretString123_abc",
      "RequireHttpsMetadata": false,
      "ApiScope": "openid profile email"
    }
  }
}
```

API (`appsettings.Development.json` or user secrets):
```json
{
  "Jwt": {
    "Authority": "https://dev-abc123.auth0.com/",
    "Audience": "https://api.residencyroll.com",
    "RequireHttpsMetadata": false
  }
}
```

---

## Common Mistakes

❌ **Wrong**: `https://dev-abc123.auth0.com` (missing trailing slash)  
✅ **Correct**: `https://dev-abc123.auth0.com/`

❌ **Wrong**: `dev-abc123.auth0.com/` (missing https://)  
✅ **Correct**: `https://dev-abc123.auth0.com/`

❌ **Wrong**: Using Web ClientId in API configuration  
✅ **Correct**: Use API Identifier in API configuration

❌ **Wrong**: Committing ClientSecret to git  
✅ **Correct**: Use user secrets or environment variables
