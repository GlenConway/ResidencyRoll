# Auth0 Setup Guide for ResidencyRoll

## Step 1: Create Auth0 Account

1. Go to https://auth0.com and sign up for a free account
2. Create a new tenant (e.g., `your-tenant-name`)
3. Your Authority URL will be: `https://your-tenant-name.auth0.com/`

## Step 2: Create API in Auth0

1. In Auth0 Dashboard, go to **Applications → APIs**
2. Click **Create API**
3. Configure:
   - **Name**: `ResidencyRoll API`
   - **Identifier**: `https://api.residencyroll.com` (or any unique identifier)
   - **Signing Algorithm**: RS256
4. Click **Create**
5. **Save the Identifier** - this is your **Audience** value

## Step 3: Create Web Application in Auth0

1. In Auth0 Dashboard, go to **Applications → Applications**
2. Click **Create Application**
3. Configure:
   - **Name**: `ResidencyRoll Web`
   - **Application Type**: Regular Web Application
4. Click **Create**
5. Go to **Settings** tab and configure:
   - **Allowed Callback URLs**: `https://localhost:5001/signin-oidc`
   - **Allowed Logout URLs**: `https://localhost:5001/`
   - **Allowed Web Origins**: `https://localhost:5001`
6. Scroll down and click **Save Changes**
7. **Copy these values**:
   - **Domain** (e.g., `your-tenant.auth0.com`)
   - **Client ID**
   - **Client Secret**

## Step 4: Configure API Permissions

1. Still in your Web Application settings
2. Go to **APIs** tab
3. Authorize the `ResidencyRoll API` you created
4. Select the permissions needed (or leave as default)

## Step 5: Configure Your Application

### Option A: Using User Secrets (Recommended for Development)

**For Web Application:**
```bash
cd src/ResidencyRoll.Web
dotnet user-secrets init
dotnet user-secrets set "Authentication:OpenIdConnect:Authority" "https://YOUR-TENANT.auth0.com/"
dotnet user-secrets set "Authentication:OpenIdConnect:ClientId" "YOUR-CLIENT-ID"
dotnet user-secrets set "Authentication:OpenIdConnect:ClientSecret" "YOUR-CLIENT-SECRET"
```

**For API:**
```bash
cd src/ResidencyRoll.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Authority" "https://YOUR-TENANT.auth0.com/"
dotnet user-secrets set "Jwt:Audience" "YOUR-API-IDENTIFIER"
```

### Option B: Update Configuration Files Directly (Not Recommended)

**⚠️ WARNING: Never commit secrets to source control!**

If you choose this option, update the placeholder values in:
- `src/ResidencyRoll.Web/appsettings.Development.json`
- `src/ResidencyRoll.Api/appsettings.Development.json`

But make sure to **git restore** these files before committing!

## Step 6: Run the Application

1. Start the API:
   ```bash
   cd src/ResidencyRoll.Api
   dotnet watch run
   ```

2. Start the Web app (in a new terminal):
   ```bash
   cd src/ResidencyRoll.Web
   dotnet watch run
   ```

3. Open your browser to the Web app URL (typically https://localhost:5001)
4. Click **Login** button
5. You'll be redirected to Auth0 login page
6. After authentication, you'll be redirected back to the app

## Configuration Reference

### Values You Need from Auth0:

| Setting | Where to Find It | Used In |
|---------|-----------------|---------|
| **Authority** | Application Domain with `https://` and trailing `/` | Both Web & API |
| **ClientId** | Application → Settings → Client ID | Web Only |
| **ClientSecret** | Application → Settings → Client Secret | Web Only |
| **Audience** | APIs → Your API → Identifier | API Only |

### Web Application Configuration:

```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "Authority": "https://YOUR-TENANT.auth0.com/",
      "ClientId": "YOUR-CLIENT-ID-FROM-AUTH0",
      "ClientSecret": "YOUR-CLIENT-SECRET-FROM-AUTH0",
      "RequireHttpsMetadata": false,
      "ApiScope": "openid profile email"
    }
  }
}
```

### API Configuration:

```json
{
  "Jwt": {
    "Authority": "https://YOUR-TENANT.auth0.com/",
    "Audience": "YOUR-API-IDENTIFIER-FROM-AUTH0",
    "RequireHttpsMetadata": false
  }
}
```

## Testing Authentication

1. **Without Auth**: You'll see a 401 Unauthorized error when accessing the API
2. **With Auth**: 
   - Click Login → Redirected to Auth0
   - Enter credentials or sign up
   - Redirected back to app
   - Your name appears in top-right corner
   - API calls now work with your JWT token

## Troubleshooting

### "Callback URL mismatch"
- Verify the callback URL in Auth0 matches exactly: `https://localhost:5001/signin-oidc`
- Make sure there's no extra `/` or missing `https://`

### "401 Unauthorized" from API
- Check that Authority and Audience match between Web and API configs
- Verify the API identifier in Auth0 matches your Audience setting
- Look at the browser's Network tab to see if the Bearer token is being sent

### "Invalid state" error
- Clear your browser cookies and try again
- Make sure RequireHttpsMetadata is set to `false` in development

### No token being sent to API
- Check that `ApiScope` includes your API identifier or scopes
- Verify the Web app is authorized to access the API in Auth0 dashboard

## Creating Test Users

1. In Auth0 Dashboard, go to **User Management → Users**
2. Click **Create User**
3. Enter email and password
4. User can now log in to your application

## Next Steps

- Add user roles and permissions in Auth0
- Configure custom claims to include in JWT tokens
- Set up social login providers (Google, GitHub, etc.)
- Configure branding for Auth0 login pages
- Set up email verification and password reset flows

## Production Deployment

When deploying to production:
1. Change `RequireHttpsMetadata` to `true`
2. Use environment variables or secret management services
3. Update callback URLs to production domain
4. Enable HTTPS everywhere
5. Review Auth0 security settings
