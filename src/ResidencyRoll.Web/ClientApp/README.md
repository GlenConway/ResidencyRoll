# Residency Roll web client

React UI for Residency Roll. The web host serves the built files and owns sign-in. During development the Vite server proxies `/api`, `/auth`, `/bff`, and `/mcp` to that host. The traveller selected in the header is stored in this browser (`residencyroll_traveller_id`). The home country for that person is also mirrored in `localStorage` (`residencyroll_home_country`).

## Scripts

```bash
cd src/ResidencyRoll.Web/ClientApp
npm ci
npm run dev
```

`npm run dev` proxies `/api`, `/auth`, `/bff`, `/signin-oidc`, and `/signout-callback-oidc` to `https://localhost:5113`. Start the web host and the API first:

```bash
dotnet run --project src/ResidencyRoll.Api
dotnet run --project src/ResidencyRoll.Web
```

Other checks: `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`.

## API types

With the API running in Development, refresh the committed OpenAPI types:

```bash
npm run gen:api
```

That writes `src/api/schema.ts` from `http://localhost:5003/swagger/v1/swagger.json`. The client currently uses the camelCase shapes in `src/api/types.ts`, which match the API's JSON.
