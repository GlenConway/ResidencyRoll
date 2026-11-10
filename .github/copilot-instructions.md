# GitHub Copilot Instructions for ResidencyRoll

The UI is a React SPA in `src/ResidencyRoll.Web/ClientApp`. The ASP.NET Core project is the BFF: it serves the built files from `wwwroot`, keeps the OpenID Connect session, and proxies `/api` with the user's access token. Do not put access tokens in browser storage.

## React

- TypeScript is strict. Import local modules with their `.ts` or `.tsx` extension.
- Keep page markup in `src/pages` and reusable pieces in `src/components`. Put date, airport, and residency rules in `src/lib` so they can be unit tested without rendering Carbon.
- Talk to the BFF through `src/api/client.ts`. Non-GET calls send `X-CSRF: 1`. A 401 redirects to `/auth/login`. Omit `travellerId` when no traveller is selected, so unscoped calls stay on `/api/v1/trips` with no query string.
- The selected traveller is `localStorage` key `residencyroll_traveller_id`. The home country for that person is mirrored in `residencyroll_home_country`. Pages without a traveller provider keep using `useHomeCountry` and the profile endpoint.
- Match the existing Tailwind and shadcn components in `src/components/ui`.
- Run `npm run lint`, `npm run typecheck`, and `npm test` from `ClientApp` before finishing UI work.

## .NET

- xUnit tests use plain `Assert`. Name them `MethodName_Condition_ShouldExpectedBehavior`.
- Do not hand-edit EF Core migrations or `ApplicationDbContextModelSnapshot.cs`.
- Never commit secrets, API keys, or `appsettings.Development.json`.

## MCP

AI clients call a stateless Streamable HTTP server at `/mcp` (ADR-016, `docs/guides/MCP_SETUP.md`). Tool classes live in `src/ResidencyRoll.Api/Mcp` and call the same services as REST. Trip, residency, forecast, presence, and home-country tools take an optional `traveller`; omit it for the signed-in person. `lookup_airport`, `get_country_rules`, and `parse_itinerary` are not per traveller. The Web host proxies `/mcp` and the OAuth protected-resource metadata without attaching the cookie token or requiring CSRF. The System Info page shows `window.location.origin + "/mcp"`. Do not add personal access tokens unless a new ADR says to.
