# GitHub Copilot Instructions for ResidencyRoll

The UI is a React SPA in `src/ResidencyRoll.Web/ClientApp`. The ASP.NET Core project is the BFF: it serves the built files from `wwwroot`, keeps the OpenID Connect session, and proxies `/api` with the user's access token. Do not put access tokens in browser storage.

## React

- TypeScript is strict. Import local modules with their `.ts` or `.tsx` extension.
- Keep page markup in `src/pages` and reusable pieces in `src/components`. Put date, airport, and residency rules in `src/lib` so they can be unit tested without rendering Carbon.
- Talk to the BFF through `src/api/client.ts`. Non-GET calls send `X-CSRF: 1`. A 401 redirects to `/auth/login`.
- The home country stays in `localStorage` under `residencyroll_home_country`.
- Match existing Carbon patterns: `Theme`, `DataTable`, `Button`, and `InlineNotification`.
- Run `npm run lint`, `npm run typecheck`, and `npm test` from `ClientApp` before finishing UI work.

## .NET

- xUnit tests use plain `Assert`. Name them `MethodName_Condition_ShouldExpectedBehavior`.
- Do not hand-edit EF Core migrations or `ApplicationDbContextModelSnapshot.cs`.
- Never commit secrets, API keys, or `appsettings.Development.json`.
