# Tech Stack

AniKo — a React SPA on Netlify and an ASP.NET Core API on Render, deployed
independently.

## Repository layout

| Path | Purpose |
|------|---------|
| `frontend/` | `aniko-frontend` — React + TypeScript + Vite SPA |
| `backend/AniKo_API/` | ASP.NET Core Minimal API, EF Core models, migrations, seeder |
| `backend/AniKo_API.Tests/` | xUnit suite |
| `backend/AniKo.slnx` | Solution file (the newer XML `slnx` format, not `.sln`) |
| `Dockerfile` | Backend-only image; `frontend/` is excluded via `.dockerignore` |
| `render.yaml` | Blueprint: `aniko-api` Docker service + `aniko-db` Postgres 17 |
| `netlify.toml` | Frontend build config, `base = "frontend"` |
| `docs/` | Phased implementation plans |

> **The two halves deploy separately and are not yet connected.** The SPA runs
> on fixtures; the API exposes only `/` and `/health`. See the status table in
> [README.md](README.md) before assuming a feature exists.

---

## Frontend

### Core
- **React 19.2.8** + **TypeScript ~6.0.2**
- **Vite 8.2** (`tsc -b && vite build`)
- **react-router-dom 7.18** — a single `DashboardShell` layout wrapping all routes

### UI & styling
- **Tailwind CSS 4.3** via `@tailwindcss/vite` — CSS-first config, no
  `tailwind.config.js`
- **@base-ui/react 1.7** as the primitive layer, following the **shadcn/ui**
  pattern (`components.json`, style `base-nova`, base colour `neutral`)
- **class-variance-authority**, **clsx**, **tailwind-merge** for variants
- **lucide-react 1.31** icons, **tw-animate-css** for keyframes

> Note this is **@base-ui/react**, not Radix directly — Base UI is the successor
> primitives library from the same team. The sibling CCTMS repo uses Radix, so
> the two are not copy-paste compatible even though both follow shadcn
> conventions.

### Data visualisation & maps
- **Recharts 3.10** — the market price trend chart
- **Leaflet 1.9** + **react-leaflet 5.0** with OpenStreetMap tiles — the nearby
  supplier map

### Internationalisation
- **i18next 26.3** + **react-i18next 17.0**
- Locales in `frontend/src/app/locales/` — `en.json` and `fil.json`
  (English, Filipino), covered by `locales.test.ts`

### State
**No global state library.** State is local component state plus custom hooks
(`useSession`, `useOverviewStats`, `useFeaturedLots`, `useRecentOrders`,
`useNearbySuppliers`, `useMarketPriceTrends`). Each hook currently returns data
from a colocated `fixtures.ts` and carries a doc comment naming the endpoint it
will later fetch — the seam where the API gets wired in.

### Tests & tooling
- **Vitest 4.1** + **jsdom 30** + Testing Library (`react` 16.3, `jest-dom` 7.0,
  `user-event` 14.6, `dom` 10.4). Config in `vitest.config.ts`, setup in
  `src/test/setup.ts`
- Near-comprehensive colocated `*.test.tsx` coverage, including accessibility
  and empty-state checks
- **oxlint 1.75** — not ESLint
- Path alias `@` → `frontend/src`

### Environment
**No `VITE_*` variables are referenced anywhere in `frontend/src`.** There is
deliberately no `frontend/.env.example`, because it would document configuration
that does not exist. The API base URL becomes the first such variable when the
data hooks are wired up.

---

## Backend

### Core
- **.NET 10** (`net10.0`), `Nullable` and `ImplicitUsings` enabled
- **ASP.NET Core Minimal API** — endpoint groups in `Endpoints/`, not MVC
  controllers
- **Microsoft.AspNetCore.OpenApi 10.0.11** + **Scalar.AspNetCore 2.16.20** for
  the interactive API reference
- **FluentValidation.DependencyInjectionExtensions 12.1.1**
- `GenerateDocumentationFile` is on (with `NoWarn 1591`), so XML doc comments
  feed the OpenAPI document

### Database
- **PostgreSQL 17** via **Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3** on
  **EF Core 10.0.11**
- One migration: `20260816072655_InitialCreate`
- Migrations run at startup via `DbInitializer.InitializeAsync`, gated by
  `Database:MigrateOnStartup` (default true)
- `Configuration/ConnectionStringResolver.cs` converts Render's
  `postgresql://user:pass@host/db` URI into the key/value form Npgsql expects —
  the two formats are not interchangeable, and this is the usual cause of a
  connection failure on first deploy

The Postgres major version is pinned to **17** in both `render.yaml` and the
local Docker command so development and production cannot drift apart.

#### Domain models
`AppUser` (+ `UserRole`: Buyer/Farmer), `Crop`, `Listing`, `Order`
(+ `OrderStatus`: Confirmed/Processing/Shipped/Delivered), `PriceObservation`,
`Supplier`, and `SeedHistory`.

`SeedHistory` exists purely as an idempotency marker — `DemoDataSeeder` checks it
so a restart does not duplicate demo rows, which matters on Render's free plan
where instances wake from idle regularly.

### Authentication
**None.** No auth middleware, no JWT or identity packages. `AppUser` and
`UserRole` exist as data only; the frontend's `useSession()` returns a hardcoded
user. This is pre-auth demo state, not a security model.

### Tests
**xUnit 2.9.3** with `Microsoft.NET.Test.Sdk 17.14.1`,
`Microsoft.AspNetCore.Mvc.Testing 10.0.11` (in-process integration testing) and
`coverlet.collector 6.0.4`.

---

## Deployment

### Frontend — Netlify
`netlify.toml` lives at the **repository root** with `base = "frontend"`, rather
than inside `frontend/`, so a clean import deploys with no dashboard settings at
all. If the site's Base directory field was already set to `frontend` when it was
created, Netlify reads config from *that* directory and ignores this file —
clear the field if a deploy still 404s.

`NODE_VERSION` is pinned to **22**. The SPA fallback (`/*` → `/index.html`,
status **200**, not 301) must stay last in the file, since Netlify applies the
first matching rule.

### Backend — Render
`render.yaml` declares `aniko-db` (free Postgres 17, Singapore) and `aniko-api`
(free Docker web service, Singapore, `autoDeploy: true`), health-checked at
`/health`, with `DATABASE_URL` supplied by `fromDatabase`.

The `Dockerfile` is a two-stage .NET build — `sdk:10.0` restores and publishes
with `/p:UseAppHost=false`, then `aspnet:10.0` runs the DLL as the non-root
`$APP_UID`.

### CI
None. `.github/` exists but contains no workflows — `npm test`, `npm run
typecheck`, `npm run lint` and `dotnet test` are run manually.
