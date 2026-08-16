# AniKo

**Harvest better. Trade together.**

A B2B agricultural marketplace for the Philippines — connecting farmers with
wholesale buyers so produce moves without the chain of middlemen that usually
sits between a field and a market stall. Buyers browse verified suppliers near
them, watch wholesale price trends by crop, and request quotes on standing lots;
farmers list what they have.

> **Portfolio demo, not open source.** The code is published so it can be read as
> a work sample, and cloned to run locally. It is not offered for reuse.
> See [LICENSE](LICENSE).

---

## ⚠️ Status: in active development

**This is a work in progress, and the README says so rather than describing an
app that does not exist yet.** As of now:

| Area | State |
|---|---|
| Buyer **Overview** dashboard | **Built** — the only complete screen |
| Marketplace, Orders, Messages, Logistics, Payments | **Placeholders** — routed, but each renders a "not built yet" panel |
| Frontend data | **Fixtures.** Every hook returns local sample data; nothing calls the API yet |
| Backend endpoints | `GET /` and `GET /health` only. No feature endpoints |
| Backend data model | **Built** — EF Core entities, initial migration, demo seeder |
| Authentication | **Not implemented.** `useSession()` returns a hardcoded user |

The frontend and backend are therefore **not yet connected**. Each data hook
carries a doc comment naming the endpoint it will eventually call
(`GET /api/v1/buyer/overview/stats`, and so on); the wiring is the next phase.

---

## What it demonstrates

Even at this stage the interesting parts are visible in the Overview dashboard:

**A dual-role marketplace.** The header carries a Buy/Sell mode switch — the same
account is a buyer or a seller depending on context, which is how agricultural
trade actually works. A farmer with surplus is a supplier on Monday and a buyer
of seed on Tuesday.

**Verification as a first-class concept.** Suppliers and lots carry a verified
badge, because the thing that stops smallholder farmers trading at wholesale is
not discovery — it is trust that the counterparty and the volume are real.

**Geography as a primary filter.** Nearby suppliers render on a Leaflet map with
distances, since freight cost on produce is a large fraction of margin and a
cheap supplier three provinces away is not cheap.

**Price transparency.** A market-trends chart shows weekly wholesale price per
kilo for rice, corn and vegetables over a selectable 3/6/12-month window — the
information asymmetry this whole category exists to close.

**Bilingual from the start.** English and Filipino (`en.json`, `fil.json`) via
i18next, not retrofitted later.

---

## Stack

| Layer | Choice |
|---|---|
| Frontend | React 19, TypeScript 6, Vite 8, Tailwind CSS v4 |
| UI | shadcn/ui pattern over `@base-ui/react`, lucide-react icons |
| Routing / i18n | react-router-dom 7, i18next + react-i18next |
| Maps / charts | Leaflet + react-leaflet, Recharts |
| Backend | ASP.NET Core Minimal API on .NET 10 |
| Database | PostgreSQL 17 via Npgsql + EF Core 10 |
| Tests | Vitest + Testing Library (frontend), xUnit (backend) |
| Lint | oxlint |

Full detail in [STACK.md](STACK.md).

---

## Running it

Requires **Node 22+**, the **.NET 10 SDK**, and **Docker** (for the local
database only).

### Frontend

```bash
cd frontend
npm install
npm run dev          # http://localhost:5173
```

That is genuinely all you need to see the app today — the Overview screen runs
entirely on fixtures, so the backend is optional until the two are wired
together.

### Backend

The local database is a disposable Postgres 17 container on port **55432** — the
non-default port is deliberate, so it cannot collide with a Postgres you already
have installed. See [`backend/README-local-db.md`](backend/README-local-db.md).

```bash
cd backend
dotnet run --project AniKo_API
```

Migrations run automatically at startup (`Database:MigrateOnStartup`, default
true), then the demo seeder populates reference data if `Seed:Demo` is set. The
seeder is idempotent — a `SeedHistory` row marks completion, so restarts do not
duplicate rows.

API reference is served by Scalar at the OpenAPI route.

### Tests

```bash
cd frontend && npm test          # Vitest
cd backend  && dotnet test       # xUnit
```

---

## Configuration

There is **no `.env` file** in this project, and no `frontend/.env.example` —
the frontend currently references no `VITE_*` variables at all, so an example
file would document configuration that does not exist.

The backend is configured the .NET way, through `appsettings.json` plus
environment variables (double underscore for nesting):

| Key | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | Postgres connection string |
| `DATABASE_URL` | Render injects a `postgresql://` URI; `ConnectionStringResolver.cs` converts it |
| `Seed__Demo` | Whether to seed demo data |
| `Database__MigrateOnStartup` | Run EF migrations at boot (default true) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |

`appsettings.Development.json` holds throwaway local credentials for the
disposable container only — there is nothing sensitive in it.

---

## Deployment

Two independent deploy targets:

- **Frontend → Netlify.** [`netlify.toml`](netlify.toml) at the repository root
  sets `base = "frontend"`, so a clean import needs no dashboard configuration.
  Live at **aniko-agri.netlify.app**; the intended home is
  `aniko.fitzdev.studio`.
- **Backend → Render.** [`render.yaml`](render.yaml) is a Blueprint describing a
  Docker web service (`aniko-api`) plus a managed Postgres 17 instance
  (`aniko-db`), health-checked at `/health`.

The root `Dockerfile` builds **only the backend** — `.dockerignore` excludes
`frontend/`, because the SPA is served by Netlify rather than by the API. This
is the opposite of the sibling Script Builder repo, where one container serves
both.

---

## Repository layout

```
frontend/           React SPA — the buyer dashboard
backend/AniKo_API/  ASP.NET Core Minimal API, EF Core models, migrations, seeder
backend/AniKo_API.Tests/  xUnit suite
Dockerfile          Backend-only image (frontend excluded via .dockerignore)
render.yaml         Render Blueprint: aniko-api + aniko-db
netlify.toml        Netlify build config for frontend/
docs/               Phased implementation plans
```

---

## A note on the data

Suppliers, lots, orders and price observations are all invented. The figures are
shaped to look like real Philippine wholesale trade — peso pricing, provincial
regions, realistic volumes per crop — because a marketplace demoed with
"Product 1" tells you nothing about whether the screen would survive contact
with an actual buyer.
