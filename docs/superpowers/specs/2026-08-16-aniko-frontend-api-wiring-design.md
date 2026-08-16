# AniKo Phase I — Wiring the Dashboard to the Live API

**Date:** 2026-08-16
**Status:** Approved for planning

## Goal

Replace the frontend's five fixture-backed hooks with real calls to the deployed API, and
fix the backend defects that make those calls return zeros on a schedule.

The dashboard at `https://aniko-agri.netlify.app` currently runs entirely on fixtures. It
contains no `fetch`, no `import.meta.env`, and no API base URL. The API at
`https://aniko-api-4emi.onrender.com` serves all five payloads. Phase I connects them.

## Context

Two constraints shape every decision here.

**The free Render instance takes 22.4 seconds to cold-start.** Measured: 0.036s to connect,
22.391s to first byte; warm is 0.096s. The connect time proves none of it is network or
DNS — it is the stopped container restarting, with migrate-on-startup running inside that
window. This exceeds the default timeout of essentially every HTTP client, so a naive
implementation renders an error page on a service that is perfectly healthy.

**The seed data is anchored to a fixed epoch while the dashboard's windows are anchored to
the wall clock.** `DemoDataSeeder.SeedEpoch` is `2026-08-01T00:00:00Z`. The two anchors drift
apart, and the drift has a date on it — see "The clock defect" below.

## Decisions taken

These were settled before this document and are not revisited by it.

1. **Direct-to-Render. No Netlify `/api/*` proxy.** A proxy would have to hold a 22.4s cold
   start open, and its failure would surface as a Netlify 502 — sending an investigation to
   the wrong provider. The decision is cheaply reversible: one environment variable plus a
   `netlify.toml` block, with no application code depending on it. `netlify.toml` already
   carries a comment reserving the insertion point above the SPA catch-all.
2. **The frontend adopts the backend's stat keys**, not the fixture's.
3. **Price trends stay monthly.** Ranges become 6 and 12 months.
4. **A hand-rolled fetch layer.** No TanStack Query, no SWR, no axios, no zod, no MSW.
5. **No keep-alive ping.** The cold start is made honest in the UI instead.
6. **Dashboard windows anchor to the latest activity in the data**, not to the wall clock.

## Non-goals

- Building any route other than `/overview`. The other five remain placeholders.
- Mutations. Every endpoint in scope is read-only; there is no write path to design.
- Authentication. `MOCK_USER` in `src/lib/session.ts` stays as it is.
- A shared client-side cache across panels. The five panels fetch independently.
- Verifying the Netlify SPA fallback on a deep-route refresh. Tracked separately.

---

# Part A — Backend

## A1. The clock defect

`OverviewStatsService` and `PriceTrendsService` both derive their windows from
`timeProvider.GetUtcNow()`. The seeded data does not move. Consequences, traced:

| Window | Anchor | Seeded span | Fails |
|---|---|---|---|
| Orders, trailing 30 days | wall clock | `SeedEpoch − 1…36 days` | ~2026-09-06 |
| Price lookback, 3 months | wall clock | 2025-09-01 … 2026-08-01 | ~2026-11-01 |
| Trends window, `months` back | wall clock | as above | degrades from ~2026-09-01 |

The order window is the urgent one. Around **2026-09-06** it slides past the most recent
seeded order and `ActiveOrders`, `Spend` and `Suppliers` all report zero at once. The four
tiles do not fail — they report zero, truthfully, about a window containing nothing.

`avgPrice` is a narrower bug than it first appears. `AveragePricesAsync` already selects
"the latest month present in the data" rather than the calendar month, and
`GetAsync_LaggingObservations_UsesTheLatestMonthPresentNotTheCalendarMonth` pins that
behaviour. The defect is one line earlier: the repository query filters
`Month >= currentMonth.AddMonths(-2)` from the wall clock, so once the clock passes roughly
2026-11 the filter matches nothing, `rows.Count == 0`, and the method short-circuits to
`(0m, 0m)` before the correct latest-month logic ever runs.

**Two integration tests are affected.** `OverviewStatsFiguresAgreeWithTheSeededOrders`
asserts `spend > 0` and resolves `TimeProvider.System`; it begins failing around 2026-09-06.
`OverviewStatsAveragePriceUsesTheLatestObservedMonth` recomputes its expectation from the
same wall-clock window as the service, so once that window empties it asserts `0m == 0m` and
passes while verifying nothing. The first is a test that will break; the second is a test
that will lie. The second is the more dangerous of the two.

## A2. `IDashboardClock`

Introduce one abstraction, in `Services/Abstractions/IDashboardClock.cs`:

```csharp
public interface IDashboardClock
{
    Task<DateTime> ReferenceNowAsync(CancellationToken cancellationToken = default);
}
```

`ReferenceNowAsync` returns the instant that dashboard windows treat as "now":

> the later of the wall clock and the most recent activity in the data — except that when
> the most recent activity is in the past, it wins.

Stated as an expression: `latestActivity ?? wallClock`, where `latestActivity` is
`MAX(orders.CreatedAt)` and the fallback covers an empty database.

The reason this is right, and not merely convenient: **with real data flowing, latest
activity *is* now**, and the behaviour is indistinguishable from reading the clock. The
abstraction does not encode "this is a demo." It encodes "windows are relative to when
things last happened," which is what a dashboard window means. The `SeedEpoch`-constant
alternative was rejected precisely because it would become wrong the day a real row is
written.

Implementation notes:

- Backed by a single `MAX(CreatedAt)` query on `orders`, cached in-process for 60 seconds.
  Five panels load together on one page view; without the cache that is five identical
  aggregate queries per visit.
- Takes `TimeProvider` for the fallback, so it stays testable with `FrozenTimeProvider`.
- Registered as a singleton alongside `TimeProvider.System` in `Program.cs`.

`OverviewStatsService` and `PriceTrendsService` both switch from
`timeProvider.GetUtcNow().UtcDateTime` to `await clock.ReferenceNowAsync(ct)`. Neither
service's window arithmetic changes otherwise.

Deliberately unchanged: `InfoEndpoints`' `timestamp` field keeps reading `DateTime.UtcNow`.
It reports when the request was served, which has nothing to do with data windows.

## A3. Trend ranges

`/api/v1/pricing/trends` keeps `months` in `[1, 24]` and its monthly pivot. Nothing in the
endpoint changes. The seed holds exactly 12 months, so 12 points is the honest ceiling; the
frontend offers 6 and 12 and drops the 3-month option, which would have rendered three
points and read as broken.

## A4. Backend tests

New, in `Services/DashboardClockTests.cs`:

- returns the latest order timestamp when it is behind the wall clock
- returns the latest order timestamp when it is ahead of the wall clock
- falls back to the wall clock on an empty database
- queries once across repeated calls inside the cache window

Changed:

- `OverviewStatsServiceTests` and `PriceTrendsServiceTests` inject a stub `IDashboardClock`
  in place of `FrozenTimeProvider`. Their frozen instant and every asserted date string stay
  as they are, so the existing assertions keep their meaning.
- `OverviewStatsFiguresAgreeWithTheSeededOrders` and
  `OverviewStatsAveragePriceUsesTheLatestObservedMonth` anchor their expectations on
  `PostgresFixture.SeedEpoch` instead of `DateTime.UtcNow`. The XML comment on the first,
  which currently explains why it *cannot* anchor on the epoch, is replaced by one
  explaining why it now can.

**Both changed integration tests must be mutation-checked**: revert `IDashboardClock` and
they must fail. A calendar bug whose regression test also degrades with the calendar is
worth nothing, and this codebase has already shipped three defects behind assertions that
had stopped measuring their subject.

---

# Part B — Frontend

## B1. Architecture

Five layers, each usable and testable without the one above it.

```
lib/api/config.ts          base URL from import.meta.env
        ↓
lib/api/client.ts          apiFetch<T>() — URL building, timeout, error classification
        ↓
lib/api/useApiResource.ts  state machine, retry/backoff, the `waking` flag
        ↓
features/<x>/adapt.ts      pure wire-DTO → client-type functions
        ↓
features/<x>/use<X>.ts     the five existing hooks, unchanged names
```

The seam already exists and the codebase was built for it. Every fixture is imported by
exactly one file — its hook — and `Overview.tsx` carries a comment stating that panels own
their own data so that Phase I can swap fixtures for fetches without the route changing.
That design holds. **Checklist item 148 is answered: panels keep owning their data hooks.**
No shared data layer, no provider, no lifting state into the route.

`adapt.ts` as *pure functions* is the load-bearing choice. Both wire-to-client shape gaps —
the stats `key → icon/labelKey/format/upIsGood` join and the nested-`Prices`-dictionary
flattening — are the parts most likely to be wrong, and keeping them pure means they are
tested with plain objects and no network at all. Only `useApiResource` needs a fetch stub.

## B2. Configuration

`src/lib/api/config.ts` exports `API_BASE_URL`, read from `import.meta.env.VITE_API_BASE_URL`.

- Development default: `http://localhost:5199`. Local work needs no `.env` file.
- Production: **throws at module load if the variable is unset.** A missing base URL would
  otherwise produce same-origin requests that Netlify answers with the SPA `index.html` at
  200 — HTML parsed as JSON, surfacing as a malformed-response error pointing nowhere near
  the actual cause. Failing loudly at load is the cheaper failure.
- Trailing slashes are stripped so `${base}${path}` cannot produce `//api/v1`.

Supporting changes: an `ImportMetaEnv` augmentation in `src/vite-env.d.ts`
(`tsconfig.app.json` already includes `vite/client`, so no config edit); a committed
`frontend/.env.example`; and `VITE_API_BASE_URL=https://aniko-api-4emi.onrender.com` set in
the Netlify site environment.

`http://localhost:5173` is already in the backend's development CORS origins and the Netlify
origin is already in production's — but **this must be verified with a real preflight
`OPTIONS` against the deployed API before any fetch code is written.** Reading the config is
not evidence. This project has spent an hour on a deploy whose every signal said it was fine.

## B3. Transport and the error taxonomy

`src/lib/api/client.ts` exports `apiFetch<T>(path, { query, signal })`. It builds the URL,
sends `Accept: application/json`, applies a **30-second per-attempt** timeout via
`AbortSignal.timeout` composed with the caller's signal, and classifies every failure into a
discriminated union:

```ts
type ApiFailure =
  | { kind: 'network' }                                  // fetch threw; offline, DNS, CORS
  | { kind: 'timeout' }                                  // exceeded the attempt budget
  | { kind: 'client'; status: number; problem?: Problem } // 4xx — our bug
  | { kind: 'server'; status: number }                   // 5xx — their bug
  | { kind: 'malformed' }                                // 2xx, unparseable or wrong shape
```

The taxonomy exists because the retry policy falls directly out of it:

| kind | retry | user sees |
|---|---|---|
| `network` | yes, backoff | retryable error with a Retry button |
| `timeout` | yes, backoff | the waking state, then a retryable error |
| `server` | yes, backoff | "we're having trouble" — no user action offered |
| `client` | **never** | our bug; surfaced, not retried |
| `malformed` | **never** | our bug; surfaced, not retried |

**`client` never retries.** The UI constructs these query strings itself, so a 400 means
*we* built the URL wrong. Retrying would burn the budget and bury the evidence under a
generic network message. `malformed` is the same category: a shape mismatch is a contract
break, and no number of retries fixes it.

`Problem` parses RFC 9457 `application/problem+json` (`type`, `title`, `status`, `detail`,
`errors`). It must tolerate a **400 with no body and no content type** — the exact shape this
API served in production before the `ThrowOnBadRequest` fix. The server is fixed; the client
should degrade to a bare status rather than throwing while trying to explain an error.

## B4. Loading, and the cold start

`src/lib/api/useApiResource.ts` exposes:

```ts
type ApiResource<T> =
  | { status: 'loading'; waking: boolean }
  | { status: 'success'; data: T }
  | { status: 'error'; failure: ApiFailure; retry: () => void }
```

Behaviour:

- **`waking` flips true at 3000ms** while still loading. Below that threshold the plain
  skeleton is the honest answer; a warm response arrives in 0.096s and never trips it.
- **Two budgets, and they must not be confused.** A **30s per-attempt** timeout, and a **45s
  overall deadline** across all attempts. Attempts are `network`/`timeout`/`server` only, up
  to 3, with 1s then 2s backoff. The deadline governs: if the first attempt burns its full
  30s and backs off 1s, the second attempt gets the remaining 14s, not another 30. Without
  the overall deadline, three 30s attempts plus backoff would leave a user staring at a
  skeleton for 97 seconds.
- The per-attempt 30s is sized on the measured 22.4s cold start with ~35% headroom; the 45s
  deadline is the point past which waiting is worse than an error with a Retry button. Both
  are judgement calls from a single measurement, and the code comment says so and records the
  measurement, so a future reader re-measures rather than guessing at intent.
- Aborts in-flight requests on unmount and ignores aborted results, which also makes it
  correct under React 19 `StrictMode` double-invocation. `main.tsx` renders inside
  `StrictMode` today, so this is exercised on every dev run rather than being theoretical.

The cold-start UX is a message, not a spinner: the existing skeleton stays on screen and an
honest line appears beside it — *"Waking the server. This can take up to 30 seconds on the
free tier."* A reviewer reading a truthful explanation forms a better impression than one
watching an unexplained spinner for twenty seconds. Delivered in `role="status"` /
`aria-live="polite"`, matching what `MarketPriceTrendsSkeleton` and `SupplierMapSkeleton`
already do.

## B5. Panel states

Four states per panel: **loading**, **waking**, **error**, **empty**. Five panels means a
shared component rather than five divergent copies.

`src/components/PanelState.tsx` renders the non-success states given a resource and a
skeleton to show while loading. Panels keep their own skeletons — a chart skeleton and a map
skeleton should not look alike — but share the waking message, the error treatment and the
retry affordance.

- Skeletons exist for pricing and the supplier map. **Three are missing** and must be built:
  stat tiles, featured lots, recent orders. Each mirrors its real content's geometry so the
  layout does not jump on resolution.
- Empty states exist for suppliers, lots and orders. **Two are missing**: stat tiles and the
  price chart.
- Error styling uses `--color-destructive`, which is defined in the theme and currently used
  by no component. `src/lib/no-raw-hex.test.ts` globs every source file and fails on raw hex
  outside `chart-theme.ts`, so new files are automatically in scope: **tokens only**.
- A `client` or `malformed` failure shows a generic message with no Retry button, since
  there is nothing the user can do. The `problem.title` renders only under `import.meta.env.DEV`.
- All new copy goes in **both** `en.json` and `fil.json`. `locales.test.ts` enforces key
  parity and will fail on a one-sided addition.

## B6. Adapters and the two shape gaps

**Overview stats.** The wire carries `OverviewStatDto(Key, Value, DeltaPercent)`. The client's
`OverviewStat` also needs `labelKey`, `icon` (a `LucideIcon`), `format` and `upIsGood`, none
of which belong on the wire — they are presentation. `features/overview/adapt.ts` holds a
static map from key to those four fields and joins it to the response.

The stat set changes, per the decision above. Fixture keys `new_inquiries`, `pending_orders`,
`saved_lots`, `spend_this_month` are replaced by the backend's `activeOrders`, `spend`,
`suppliers`, `avgPrice`. `saved_lots` and `new_inquiries` have no backing tables and could
only ever have been faked. This requires new icons, new `en`/`fil` copy, and a rewrite of
`useOverviewStats.test.ts`, which asserts the old keys exactly — the test doing its job.

An unknown key from the wire is **dropped with a `console.warn`, not rendered blank**. A tile
with no label is worse than four tiles.

**Price trends.** The wire is `PricePointDto(Date, IReadOnlyDictionary<string, decimal>)`;
the client's `PricePoint` is flat `{ date, rice, corn, vegetables }`. `features/pricing/adapt.ts`
flattens it, defaulting an absent crop to `0`, matching the backend's own `MissingPrice`
convention. `RANGE_MONTHS` becomes `[6, 12]` and `WEEKS_PER_RANGE` is deleted.

**The other three are 1:1** and need only a null-safe mapping. Note that
`useFeaturedLots.ts`'s doc comment names `GET /api/v1/lots/featured`; the route is
`/listings/featured`. Correct the comment.

## B7. The hook contract changes

Each hook's doc comment currently promises its return shape will not change. That promise is
deliberately broken:

```ts
// before
{ stats: OverviewStat[]; isLoading: boolean }
// after
{ resource: ApiResource<OverviewStat[]> }
```

`isLoading` was a hardcoded `false` that no component ever read, and it could not express
"failed," which is the state that matters most on a free-tier deployment. Recording the break
here rather than letting a future reader find a violated comment.

Consequence: all five hook tests are synchronous `renderHook` calls, several asserting
`isLoading === false` by name. They become async. Several panel tests follow. Roughly ten
test files churn.

## B8. Frontend tests

Vitest, jsdom, Testing Library — all already configured. **No MSW**; a ~30-line `fetch` stub
installed per test, consistent with choosing not to add a fetching library.

- **Adapters** — pure input/output. Both shape gaps, unknown keys, missing crops, empty lists.
- **Client** — one test per `ApiFailure` kind, plus problem+json parsing and the body-less 400.
- **`useApiResource`** — fake timers: backoff sequence, no-retry-on-4xx, `waking` at 3s,
  budget exhaustion, abort on unmount, `StrictMode` double-invoke.
- **Panels** — loading, waking, error-with-retry, error-without-retry, empty, success.
- Unchanged and still enforced: `no-raw-hex.test.ts`, `locales.test.ts` parity,
  `palette.test.ts`.

`src/test/setup.ts` carries a warning against adding a `ResizeObserver` polyfill — Recharts 3
only renders in jsdom because it is absent. **Nothing in this phase may add one.**

---

## Risks

| Risk | Handling |
|---|---|
| Render serves a stale build and the frontend appears broken | Check `commit` on `/` before diagnosing anything client-side. This is why that field exists. |
| CORS fails in production despite correct config | Real preflight `OPTIONS` before writing fetch code (B2). |
| 3s / 45s are guesses from one measurement | Declared as such in comments, with the measurement recorded so they can be re-derived. |
| The changed integration tests still degrade with the calendar | Mutation-check both: revert `IDashboardClock`, they must fail (A4). |
| `aniko-db` free tier expires 2026-09-15, no backups | Outside this phase, but it is 30 days out and the clock fix lands before it. |

## Success criteria

1. A cold visit to `https://aniko-agri.netlify.app/overview` renders every panel with live
   API data, showing skeletons and then the waking message, without an error state.
2. A warm visit renders in under a second with no waking message.
3. With the API stopped, every panel shows a retryable error and Retry recovers once it is up.
4. The four stat tiles show non-zero values, and would continue to on 2026-10-01.
5. Full backend suite green, mutation-checked on the clock change.
6. Full frontend suite green, including hex, locale-parity and palette guards.
7. `npm run build` and `npm run typecheck` clean.
