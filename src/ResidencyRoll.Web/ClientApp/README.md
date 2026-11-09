# Residency Roll web client

React UI for Residency Roll. Until cut-over it runs on the Vite dev server and talks to the existing web host, which proxies `/api` and owns sign-in.

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
