#!/bin/bash

# Auth0 Configuration Script for ResidencyRoll
# This script helps you configure Auth0 credentials using .NET User Secrets

echo "======================================"
echo "ResidencyRoll Auth0 Configuration"
echo "======================================"
echo ""
echo "This script will configure your Auth0 credentials using .NET User Secrets."
echo "Your secrets will be stored securely and NOT committed to source control."
echo ""

# Check if user wants to continue
read -p "Do you want to continue? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Configuration cancelled."
    exit 1
fi

echo ""
echo "Please enter your Auth0 credentials:"
echo ""

# Get Auth0 Tenant Domain
read -p "Auth0 Tenant Domain (e.g., your-tenant.auth0.com): " AUTH0_DOMAIN
if [[ ! $AUTH0_DOMAIN =~ auth0\.com$ ]]; then
    AUTH0_DOMAIN="${AUTH0_DOMAIN}.auth0.com"
fi
AUTH0_AUTHORITY="https://${AUTH0_DOMAIN}/"

# Get Web Application credentials
echo ""
echo "--- Web Application (from Auth0 Dashboard → Applications → ResidencyRoll Web) ---"
read -p "Client ID: " WEB_CLIENT_ID
read -sp "Client Secret: " WEB_CLIENT_SECRET
echo ""

# Get API credentials
echo ""
echo "--- API Configuration (from Auth0 Dashboard → Applications → APIs → ResidencyRoll API) ---"
read -p "API Identifier (Audience): " API_AUDIENCE

echo ""
echo "======================================"
echo "Configuring secrets..."
echo "======================================"

# Configure Web Application
echo ""
echo "Configuring Web Application..."
cd "$(dirname "$0")/src/ResidencyRoll.Web" || exit

if ! dotnet user-secrets list >/dev/null 2>&1; then
    echo "Initializing user secrets for Web..."
    dotnet user-secrets init
fi

dotnet user-secrets set "Authentication:OpenIdConnect:Authority" "$AUTH0_AUTHORITY"
dotnet user-secrets set "Authentication:OpenIdConnect:ClientId" "$WEB_CLIENT_ID"
dotnet user-secrets set "Authentication:OpenIdConnect:ClientSecret" "$WEB_CLIENT_SECRET"

echo "✓ Web application configured"

# Configure API
echo ""
echo "Configuring API..."
cd "../ResidencyRoll.Api" || exit

if ! dotnet user-secrets list >/dev/null 2>&1; then
    echo "Initializing user secrets for API..."
    dotnet user-secrets init
fi

dotnet user-secrets set "Jwt:Authority" "$AUTH0_AUTHORITY"
dotnet user-secrets set "Jwt:Audience" "$API_AUDIENCE"

echo "✓ API configured"

echo ""
echo "======================================"
echo "Configuration Complete!"
echo "======================================"
echo ""
echo "Your Auth0 credentials have been securely stored in .NET User Secrets."
echo ""
echo "Configuration Summary:"
echo "  Authority: $AUTH0_AUTHORITY"
echo "  Web Client ID: $WEB_CLIENT_ID"
echo "  API Audience: $API_AUDIENCE"
echo ""
echo "To verify your configuration:"
echo "  cd src/ResidencyRoll.Web && dotnet user-secrets list"
echo "  cd src/ResidencyRoll.Api && dotnet user-secrets list"
echo ""
echo "To start the application:"
echo "  1. Terminal 1: cd src/ResidencyRoll.Api && dotnet watch run"
echo "  2. Terminal 2: cd src/ResidencyRoll.Web && dotnet watch run"
echo ""
echo "Then open https://localhost:5001 in your browser and click Login!"
echo ""
