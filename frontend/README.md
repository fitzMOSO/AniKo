# AniKo — Frontend

React + TypeScript + Vite SPA for AniKo, the buyer/seller marketplace dashboard.

See the [repository README](../README.md) for what the app does and what is
actually built, and [STACK.md](../STACK.md) for full dependency detail.

## Setup

```bash
npm install
npm run dev          # http://localhost:5173
```

**The backend is not required.** Every data hook currently returns local
fixtures, so the app runs standalone — see the status table in the repository
README.

**No environment file is needed.** The app references no `VITE_*` variables
today; there is deliberately no `.env.example`, since it would document
configuration that does not exist. An API base URL becomes the first one when
the hooks are wired to the backend.

## Scripts

| Script | Does |
|---|---|
| `dev` | Vite dev server on `:5173` |
| `build` | `tsc -b && vite build` — type errors fail the build |
| `typecheck` | `tsc -b --noEmit`, without producing a bundle |
| `lint` | `oxlint` — not ESLint |
| `test` | `vitest run` |
| `test:watch` | `vitest` in watch mode |
| `preview` | Serve the production build locally |

## Structure

```
src/app/           Nav definition and i18n locales (locales/en.json, locales/fil.json)
src/layouts/       DashboardShell — the single layout wrapping every route
src/features/      One folder per dashboard section, each with its own fixtures.ts
src/components/    Shared UI, shadcn-pattern over @base-ui/react
src/lib/           Helpers and utilities
src/test/setup.ts  Vitest + Testing Library setup
```

### Fixtures are the API seam

Hooks such as `useOverviewStats`, `useFeaturedLots`, `useRecentOrders`,
`useNearbySuppliers` and `useMarketPriceTrends` each read from a colocated
`fixtures.ts` and carry a doc comment naming the endpoint they will eventually
call. Keep that shape when adding a feature — the swap to a real fetch should
touch the hook only, never the components that consume it.

## Internationalisation

English and Filipino, via i18next. Locale files live in `src/app/locales/`.

`locales.test.ts` checks the two files for key parity, so a string added to
`en.json` without a `fil.json` counterpart fails the suite rather than silently
falling back to English in production.

## Deployment

Built and published by Netlify using [`netlify.toml`](../netlify.toml) at the
**repository root** — the config is not in this directory. It sets
`base = "frontend"`, so a clean import needs no dashboard settings.
