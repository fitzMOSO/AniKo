# AniKo Phase I — Frontend API Wiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the frontend's five fixture-backed hooks with real calls to the deployed API, and fix the backend clock defect that makes those calls return zeros on a schedule.

**Architecture:** Part A introduces `IDashboardClock`, which resolves "now" for dashboard windows from the latest activity in the data rather than the wall clock, and rewires the two services that read the clock. Part B adds four frontend layers bottom-up — config, transport, a resource hook with cold-start-aware retry, and pure per-feature adapters — behind the five existing hooks, whose names and call sites do not change.

**Tech Stack:** .NET 10 minimal API, EF Core 10 + Npgsql, xUnit, Testcontainers.PostgreSql. React 19.2, Vite 8.2, TypeScript ~6.0, react-router-dom 7.18, vitest 4.1 + jsdom + @testing-library/react, oxlint, Tailwind v4, recharts 3, leaflet.

**Spec:** `docs/superpowers/specs/2026-08-16-aniko-frontend-api-wiring-design.md`

## Global Constraints

- **Single linear history on `main`.** No branches, no merge commits. Conventional commits.
- **Every commit message ends with the trailer** `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
- **Backend baseline: 407/407 tests passing, 0 warnings, 0 errors.** Every backend task must end at or above that count with zero warnings.
- **`no-raw-hex.test.ts` globs all of `frontend/src/**/*.{ts,tsx}` and fails on any raw hex literal outside `chart-theme.ts`.** Every new component uses CSS variable tokens only. The error token is `--color-destructive` (defined in `src/index.css:72`, currently used by no component).
- **`locales.test.ts` enforces exact key parity between `src/app/locales/en.json` and `src/app/locales/fil.json`.** Every added key goes in BOTH files; every removed key comes out of BOTH.
- **NEVER add a `ResizeObserver` polyfill or mock.** `src/test/setup.ts` carries an explicit warning: Recharts 3 only renders in jsdom because `ResizeObserver` is absent.
- **No React context provider may be introduced.** `App.test.tsx` and every panel test render bare under `MemoryRouter` with no provider. The hand-rolled fetch layer was chosen partly to preserve this.
- **No new runtime dependencies.** No TanStack Query, no SWR, no axios, no zod, no MSW. `package.json` dependencies must be unchanged at the end of this plan.
- **Backend routes are `/api/v1/buyer/overview/stats`, `/api/v1/pricing/trends`, `/api/v1/suppliers/nearby`, `/api/v1/listings/featured`, `/api/v1/orders/recent`.** Note `listings`, not `lots`.
- **Deployed API:** `https://aniko-api-4emi.onrender.com`. **Deployed frontend:** `https://aniko-agri.netlify.app`. **Local API:** `http://localhost:5199`.
- **Before diagnosing any client-side problem against the deployed API, `curl` the `commit` field on `/`.** Render has served a stale binary while reporting the deploy live; that field exists to make it one command.
- Backend commands run from the repo root. If a build fails with MSB3021/MSB3026/MSB3027, a stray `.NET Host` process is holding the output directory — find it with `tasklist | grep -i dotnet` and stop before retrying.
- Frontend commands run from `frontend/`. `npm test` = `vitest run`, `npm run typecheck` = `tsc -b --noEmit`, `npm run lint` = `oxlint`, `npm run build` = `tsc -b && vite build`.

---

## File Structure

### Part A — Backend

| File | Responsibility |
|---|---|
| `backend/AniKo_API/Services/Abstractions/IDashboardClock.cs` (create) | The one-method interface dashboard services depend on for "now". |
| `backend/AniKo_API/Services/DashboardClock.cs` (create) | Resolves latest-activity-or-wall-clock; consults the cache. Scoped. |
| `backend/AniKo_API/Services/DashboardClockCache.cs` (create) | 60-second in-process cache shared across requests. Singleton. |
| `backend/AniKo_API/Repositories/Abstractions/IOrderRepository.cs` (modify) | Adds `LatestCreatedAtAsync`. |
| `backend/AniKo_API/Repositories/OrderRepository.cs` (modify) | Implements it as a single `MAX(created_at)`. |
| `backend/AniKo_API/Services/OverviewStatsService.cs` (modify) | Swaps `TimeProvider` for `IDashboardClock`. |
| `backend/AniKo_API/Services/PriceTrendsService.cs` (modify) | Same swap. |
| `backend/AniKo_API/Program.cs` (modify) | Registers the cache and the clock. |
| `backend/AniKo_API.Tests/Services/ServiceTestDoubles.cs` (modify) | Adds `FakeOrderRepository.LatestCreatedAt` and `StubDashboardClock`. |
| `backend/AniKo_API.Tests/Services/DashboardClockTests.cs` (create) | Four unit tests for the clock. |
| `backend/AniKo_API.Tests/Services/OverviewStatsServiceTests.cs` (modify) | Injects `StubDashboardClock`. |
| `backend/AniKo_API.Tests/Services/PriceTrendsServiceTests.cs` (modify) | Injects `StubDashboardClock`. |
| `backend/AniKo_API.Tests/Endpoints/DashboardEndpointsHappyPathTests.cs` (modify) | Two tests re-anchored on `PostgresFixture.SeedEpoch`. |

### Part B — Frontend

| File | Responsibility |
|---|---|
| `frontend/src/vite-env.d.ts` (create) | `ImportMetaEnv` augmentation for `VITE_API_BASE_URL`. |
| `frontend/.env.example` (create) | Documents the one variable. |
| `frontend/src/lib/api/config.ts` (create) | Resolves and validates `API_BASE_URL`. |
| `frontend/src/lib/api/config.test.ts` (create) | Trailing-slash and dev-default behaviour. |
| `frontend/src/lib/api/failures.ts` (create) | `ApiFailure` union, `Problem`, `ApiError`. |
| `frontend/src/lib/api/client.ts` (create) | `apiFetch<T>` — URL build, 30s timeout, failure classification. |
| `frontend/src/lib/api/client.test.ts` (create) | One test per failure kind + problem+json + bodiless 400. |
| `frontend/src/lib/api/useApiResource.ts` (create) | State machine, retry/backoff, deadline, `waking` flag. |
| `frontend/src/lib/api/useApiResource.test.ts` (create) | Fake-timer tests for every branch. |
| `frontend/src/lib/api/testing.ts` (create) | ~30-line `fetch` stub used by the tests above. |
| `frontend/src/components/PanelState.tsx` (create) | Shared loading/waking/error rendering for all five panels. |
| `frontend/src/components/PanelState.test.tsx` (create) | Six state assertions. |
| `frontend/src/features/overview/StatTilesSkeleton.tsx` (create) | Missing skeleton. |
| `frontend/src/features/lots/FeaturedLotsSkeleton.tsx` (create) | Missing skeleton. |
| `frontend/src/features/orders/RecentOrdersSkeleton.tsx` (create) | Missing skeleton. |
| `frontend/src/features/<x>/adapt.ts` ×5 (create) | Pure wire-DTO → client-type functions. |
| `frontend/src/features/<x>/adapt.test.ts` ×5 (create) | Pure input/output tests, no network. |
| `frontend/src/features/<x>/use<X>.ts` ×5 (modify) | Fetch instead of fixture; return `ApiResource<T>`. |
| `frontend/src/features/<x>/*Panel.tsx` ×5 (modify) | Early-return `PanelState` on non-success. |
| `frontend/src/app/locales/{en,fil}.json` (modify) | New stat labels, panel state copy; removed `range_3` and old stat keys. |
| `frontend/src/features/*/fixtures.ts` ×5 (delete at Task 16) | No longer imported by anything but tests. |

---

## Task 1: `IDashboardClock`

**Files:**
- Create: `backend/AniKo_API/Services/Abstractions/IDashboardClock.cs`
- Create: `backend/AniKo_API/Services/DashboardClockCache.cs`
- Create: `backend/AniKo_API/Services/DashboardClock.cs`
- Modify: `backend/AniKo_API/Repositories/Abstractions/IOrderRepository.cs`
- Modify: `backend/AniKo_API/Repositories/OrderRepository.cs`
- Modify: `backend/AniKo_API/Program.cs` (after the `AddSingleton(TimeProvider.System)` block at lines 80-86)
- Modify: `backend/AniKo_API.Tests/Services/ServiceTestDoubles.cs`
- Test: `backend/AniKo_API.Tests/Services/DashboardClockTests.cs`

**Interfaces:**
- Consumes: `IOrderRepository`, `TimeProvider` (already a registered singleton).
- Produces: `IDashboardClock.ReferenceNowAsync(CancellationToken) → Task<DateTime>`; `DashboardClockCache` with `bool TryGet(out DateTime)` and `void Set(DateTime)`; `IOrderRepository.LatestCreatedAtAsync(CancellationToken) → Task<DateTime?>`; `StubDashboardClock(DateTime)` in the test doubles.

- [ ] **Step 1: Write the failing tests**

Create `backend/AniKo_API.Tests/Services/DashboardClockTests.cs`:

```csharp
using AniKo_API.Services;

namespace AniKo_API.Tests.Services;

/// <summary>
/// The clock exists because every dashboard window was anchored to the wall clock while the
/// seeded data is anchored to a fixed epoch. The two drift apart, and around 2026-09-06 the
/// 30-day order window slides past the newest seeded order and three tiles report zero at once.
/// </summary>
public class DashboardClockTests
{
    private static readonly DateTime WallClock = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

    private static DashboardClock Build(DateTime? latestOrder, out FakeOrderRepository repository)
    {
        repository = new FakeOrderRepository { LatestCreatedAt = latestOrder };
        var timeProvider = new FrozenTimeProvider(WallClock);
        return new DashboardClock(repository, new DashboardClockCache(timeProvider), timeProvider);
    }

    [Fact]
    public async Task ReferenceNow_LatestActivityBehindTheWallClock_UsesTheActivity()
    {
        var latest = new DateTime(2026, 7, 31, 3, 0, 0, DateTimeKind.Utc);
        var clock = Build(latest, out _);

        Assert.Equal(latest, await clock.ReferenceNowAsync());
    }

    [Fact]
    public async Task ReferenceNow_LatestActivityAheadOfTheWallClock_StillUsesTheActivity()
    {
        // A future-dated row is not rejected anywhere (see IOrderRepository.ListSinceAsync), so
        // "the later of the two" and "the activity" differ here. The activity wins either way:
        // a window that excluded the newest order would be a window with a hole at its top.
        var latest = new DateTime(2027, 1, 4, 0, 0, 0, DateTimeKind.Utc);
        var clock = Build(latest, out _);

        Assert.Equal(latest, await clock.ReferenceNowAsync());
    }

    [Fact]
    public async Task ReferenceNow_EmptyDatabase_FallsBackToTheWallClock()
    {
        var clock = Build(null, out _);

        Assert.Equal(WallClock, await clock.ReferenceNowAsync());
    }

    [Fact]
    public async Task ReferenceNow_RepeatedCalls_QueryOnceInsideTheCacheWindow()
    {
        var latest = new DateTime(2026, 7, 31, 3, 0, 0, DateTimeKind.Utc);
        var clock = Build(latest, out var repository);

        await clock.ReferenceNowAsync();
        await clock.ReferenceNowAsync();
        await clock.ReferenceNowAsync();

        // Five panels load on one page view, each in its own request scope. Without a shared
        // cache that is five identical MAX(created_at) queries per visit.
        Assert.Equal(1, repository.LatestCreatedAtCalls);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/AniKo_API.Tests --filter "FullyQualifiedName~DashboardClockTests"`
Expected: FAIL to compile — `DashboardClock`, `DashboardClockCache` and `FakeOrderRepository.LatestCreatedAt` do not exist.

- [ ] **Step 3: Add the repository method to the interface**

In `backend/AniKo_API/Repositories/Abstractions/IOrderRepository.cs`, add inside the interface, after `ListSinceAsync`:

```csharp
    /// <summary>
    /// When the most recent order was placed, or <c>null</c> if there are none.
    /// </summary>
    /// <remarks>
    /// This is what <see cref="AniKo_API.Services.IDashboardClock"/> anchors its windows on. A
    /// single <c>MAX(created_at)</c> rather than <c>ListRecentAsync(1)</c>: the latter projects a
    /// row through two joins to read one timestamp off it.
    /// </remarks>
    Task<DateTime?> LatestCreatedAtAsync(CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement it**

In `backend/AniKo_API/Repositories/OrderRepository.cs`, add after `ListSinceAsync`:

```csharp
    /// <inheritdoc/>
    public async Task<DateTime?> LatestCreatedAtAsync(CancellationToken cancellationToken = default)
    {
        // Projected to a nullable before Max, not Max over a non-nullable. EF translates the
        // former to a plain MAX() that yields NULL on an empty table; the latter throws
        // InvalidOperationException on the empty sequence, turning "no orders yet" into a 500.
        return await Query()
            .Select(o => (DateTime?)o.CreatedAt)
            .MaxAsync(cancellationToken);
    }
```

- [ ] **Step 5: Write the interface**

Create `backend/AniKo_API/Services/Abstractions/IDashboardClock.cs`:

```csharp
namespace AniKo_API.Services;

/// <summary>
/// The instant that dashboard windows treat as "now".
/// </summary>
/// <remarks>
/// <para>
/// Not <see cref="TimeProvider"/>, and the difference is the whole point. A trailing 30-day
/// window read off the wall clock is correct only while data keeps arriving. Against a fixed
/// seed it drifts: the seeded orders span <c>SeedEpoch - 1..36 days</c> from 2026-08-01, so
/// around 2026-09-06 the window slides past the newest of them and the active-orders, spend and
/// distinct-supplier tiles all report zero on the same day. They do not fail — they truthfully
/// report a window containing nothing, which is why no test and no alert would have caught it.
/// </para>
/// <para>
/// The resolution is <c>latestActivity ?? wallClock</c>. This is not a demo affordance: with
/// real orders arriving, the latest activity <i>is</i> now and the behaviour is
/// indistinguishable from reading the clock. What it encodes is that a dashboard window is
/// relative to when things last happened, which is what such a window means. Anchoring on the
/// <c>SeedEpoch</c> constant was rejected for the opposite reason — it becomes wrong the day a
/// real row is written.
/// </para>
/// </remarks>
public interface IDashboardClock
{
    Task<DateTime> ReferenceNowAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Write the cache**

Create `backend/AniKo_API/Services/DashboardClockCache.cs`:

```csharp
namespace AniKo_API.Services;

/// <summary>
/// A 60-second in-process cache for the resolved reference instant.
/// </summary>
/// <remarks>
/// Singleton, while <see cref="DashboardClock"/> is scoped, and that split is the reason this
/// class exists at all. One page view issues five requests, each with its own DI scope and its
/// own <c>IOrderRepository</c>; a cache living on the clock would be a cache with a hit rate of
/// zero. Staleness costs nothing here: the value feeds 30-day and multi-month windows, so a
/// figure up to a minute old moves no boundary.
/// </remarks>
public sealed class DashboardClockCache(TimeProvider timeProvider)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly Lock _gate = new();
    private DateTime _value;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public bool TryGet(out DateTime value)
    {
        lock (_gate)
        {
            value = _value;
            return timeProvider.GetUtcNow() < _expiresAt;
        }
    }

    public void Set(DateTime value)
    {
        lock (_gate)
        {
            _value = value;
            _expiresAt = timeProvider.GetUtcNow() + Ttl;
        }
    }
}
```

- [ ] **Step 7: Write the clock**

Create `backend/AniKo_API/Services/DashboardClock.cs`:

```csharp
using AniKo_API.Repositories;

namespace AniKo_API.Services;

/// <inheritdoc cref="IDashboardClock"/>
public sealed class DashboardClock(
    IOrderRepository orders,
    DashboardClockCache cache,
    TimeProvider timeProvider) : IDashboardClock
{
    public async Task<DateTime> ReferenceNowAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGet(out var cached))
        {
            return cached;
        }

        var latest = await orders.LatestCreatedAtAsync(cancellationToken).ConfigureAwait(false);

        // The resolved value is cached, not the nullable query result, so an empty database does
        // not re-query on every request just to fall through to the same clock reading.
        var resolved = latest ?? timeProvider.GetUtcNow().UtcDateTime;

        cache.Set(resolved);
        return resolved;
    }
}
```

- [ ] **Step 8: Register both in `Program.cs`**

In `backend/AniKo_API/Program.cs`, immediately after the existing `builder.Services.AddSingleton(TimeProvider.System);` line (currently line 86), add:

```csharp

// The dashboard's own clock, which is not the system clock. See IDashboardClock: every window
// on this dashboard is defined relative to "now", and reading that off the wall clock is only
// correct while data keeps arriving. The cache is a singleton because one page view is five
// separate requests and therefore five separate scopes; the clock is scoped because it needs a
// scoped repository.
builder.Services.AddSingleton<DashboardClockCache>();
builder.Services.AddScoped<IDashboardClock, DashboardClock>();
```

If `Program.cs` does not already have `using AniKo_API.Services;`, add it.

- [ ] **Step 9: Extend the test doubles**

In `backend/AniKo_API.Tests/Services/ServiceTestDoubles.cs`, add these members to `FakeOrderRepository` (after `LastRecentLimit`):

```csharp
    public DateTime? LatestCreatedAt { get; init; }

    /// <summary>How many times the clock actually hit the database, so the cache is assertable.</summary>
    public int LatestCreatedAtCalls { get; private set; }
```

and add this method to the same class:

```csharp
    public Task<DateTime?> LatestCreatedAtAsync(CancellationToken cancellationToken = default)
    {
        LatestCreatedAtCalls++;
        return Task.FromResult(LatestCreatedAt);
    }
```

Then add this class at the end of the file:

```csharp
/// <summary>
/// A dashboard clock pinned to a chosen instant, for services that only care what "now" is.
/// </summary>
/// <remarks>
/// The services under test used to take <see cref="FrozenTimeProvider"/> directly. They take
/// this now, which is a strictly narrower dependency: a service that computes windows should not
/// also be able to read the wall clock, because that is precisely the capability that produced
/// the drift <see cref="AniKo_API.Services.IDashboardClock"/> exists to remove.
/// </remarks>
internal sealed class StubDashboardClock(DateTime referenceNow) : AniKo_API.Services.IDashboardClock
{
    public Task<DateTime> ReferenceNowAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(DateTime.SpecifyKind(referenceNow, DateTimeKind.Utc));
}
```

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet test backend/AniKo_API.Tests --filter "FullyQualifiedName~DashboardClockTests"`
Expected: PASS, 4 tests.

- [ ] **Step 11: Run the full backend suite**

Run: `dotnet test backend/AniKo_API.Tests`
Expected: PASS, 411 tests (407 + 4), 0 warnings.

- [ ] **Step 12: Commit**

```bash
git add backend/AniKo_API/Services backend/AniKo_API/Repositories backend/AniKo_API/Program.cs backend/AniKo_API.Tests/Services
git commit -m "feat(backend): add IDashboardClock, anchoring windows to the data

Dashboard windows read the wall clock while the seed is fixed at
2026-08-01, so around 2026-09-06 the 30-day order window slides past
the newest seeded order and three tiles report zero at once.

Resolves now as latestActivity ?? wallClock. Not a demo affordance:
with real orders arriving the two are the same value.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: Rewire the two services onto the clock

**Files:**
- Modify: `backend/AniKo_API/Services/OverviewStatsService.cs:25-28` (constructor), `:66-81` (the clock read)
- Modify: `backend/AniKo_API/Services/PriceTrendsService.cs:21-24` (constructor), `:55-61` (the clock read)
- Test: `backend/AniKo_API.Tests/Services/OverviewStatsServiceTests.cs`, `backend/AniKo_API.Tests/Services/PriceTrendsServiceTests.cs`
- Test: `backend/AniKo_API.Tests/Endpoints/DashboardEndpointsHappyPathTests.cs` — the two wall-clock-anchored tests

**Corrected during execution.** This task originally stopped at the service swap, leaving the
integration-test re-anchor to Task 3, on the belief that both integration tests would keep passing
in between. They do not: `OverviewStatsFiguresAgreeWithTheSeededOrders` fails immediately with
`Expected: 3, Actual: 6`, because the service's data-anchored window now catches six non-delivered
seeded orders while the test's `DateTime.UtcNow` window catches three. The service change and the
test re-anchor are therefore **one atomic change** — separating them puts `main` through a red
commit. The re-anchors moved here; Task 3 is now the mutation check alone.

**Interfaces:**
- Consumes: `IDashboardClock.ReferenceNowAsync(CancellationToken) → Task<DateTime>` and `StubDashboardClock(DateTime)` from Task 1.
- Produces: nothing new. Both services keep their public signatures — `OverviewStatsService.GetAsync(CancellationToken)` and `PriceTrendsService.GetAsync(int, CancellationToken)`.

- [ ] **Step 1: Update the two service test files to construct with the stub clock**

Both test files declare `private static readonly DateTime Now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);` — **leave that line and every asserted date string exactly as they are.** Only the construction changes.

In `OverviewStatsServiceTests.cs`, replace every occurrence of `new FrozenTimeProvider(Now)` in a `new OverviewStatsService(...)` call with `new StubDashboardClock(Now)`.

In `PriceTrendsServiceTests.cs`, replace every occurrence of `new FrozenTimeProvider(Now)` in a `new PriceTrendsService(...)` call with `new StubDashboardClock(Now)`.

Find them with:

```bash
grep -n "FrozenTimeProvider" backend/AniKo_API.Tests/Services/OverviewStatsServiceTests.cs backend/AniKo_API.Tests/Services/PriceTrendsServiceTests.cs
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/AniKo_API.Tests --filter "FullyQualifiedName~OverviewStatsServiceTests|FullyQualifiedName~PriceTrendsServiceTests"`
Expected: FAIL to compile — the services' constructors still take `TimeProvider`.

- [ ] **Step 3: Change `OverviewStatsService`**

Replace the constructor at lines 25-28:

```csharp
public sealed class OverviewStatsService(
    IOrderRepository orders,
    IPriceObservationRepository priceObservations,
    IDashboardClock clock) : IOverviewStatsService
```

Replace line 68 inside `GetAsync`:

```csharp
        var now = await clock.ReferenceNowAsync(cancellationToken).ConfigureAwait(false);
```

Then replace the `<para>` block at lines 11-17 of the class XML doc with:

```csharp
/// <para>
/// <b><see cref="IDashboardClock"/> rather than <c>DateTime.UtcNow</c> or <see cref="TimeProvider"/>.</b>
/// Every figure here is defined relative to "now", so a service that reads the ambient clock is
/// a service whose output cannot be asserted. It is also a service that drifts: these windows
/// are trailing, the seeded data is not, and reading the wall clock is what made three of these
/// four tiles due to report zero on 2026-09-06. The clock resolves "now" from the latest
/// activity in the data instead — see <see cref="IDashboardClock"/>.
/// </para>
```

Nothing else in the file changes. In particular **do not touch `AveragePricesAsync`'s
latest-month-present-in-the-data logic** (lines 149-167); it is already correct, and
`GetAsync_LaggingObservations_UsesTheLatestMonthPresentNotTheCalendarMonth` pins it. The defect
was only ever the `firstMonth` filter at line 138, which is fixed by `now` becoming the
data-anchored instant.

- [ ] **Step 4: Change `PriceTrendsService`**

Replace the constructor at lines 21-24:

```csharp
public sealed class PriceTrendsService(
    IPriceObservationRepository priceObservations,
    ICropRepository crops,
    IDashboardClock clock) : IPriceTrendsService
```

Replace line 60 inside `GetAsync`:

```csharp
        var now = await clock.ReferenceNowAsync(cancellationToken).ConfigureAwait(false);
```

**Keep lines 57-59 verbatim** — the comment explaining that `months` is validated and must not be
clamped here. It is about the `months` argument, not about the clock, and it is still true.

Then append to the class-level `<remarks>`, after the existing `</para>`:

```csharp
/// <para>
/// The month axis comes from <see cref="IDashboardClock"/>, not the wall clock. A window
/// counted back from a wall-clock "now" over data that stops at a fixed epoch produces a chart
/// whose right-hand months are all <see cref="MissingPrice"/> — every series pinned to the floor,
/// which reads as a market crash rather than as an empty window.
/// </para>
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/AniKo_API.Tests --filter "FullyQualifiedName~OverviewStatsServiceTests|FullyQualifiedName~PriceTrendsServiceTests"`
Expected: PASS, 21 tests (11 + 10). Every asserted date string is unchanged, which is the point —
the stub feeds the same instant the frozen `TimeProvider` used to.

- [ ] **Step 6: Re-anchor `OverviewStatsFiguresAgreeWithTheSeededOrders`**

This test currently fails — `Expected: 3, Actual: 6`. The service is right and the test is stale:
it computes its expectation from `DateTime.UtcNow` while the service now resolves `now` from the
data. Both sides used to read the wall clock, which is the only reason it ever passed.

In `backend/AniKo_API.Tests/Endpoints/DashboardEndpointsHappyPathTests.cs`, replace
`var windowStart = DateTime.UtcNow.AddDays(-30);` with:

```csharp
        // Anchored on the seed epoch, not DateTime.UtcNow — see the doc comment above. The
        // service resolves the same instant through IDashboardClock, so this expectation and the
        // figure it checks are computed over the same 30 days on every future run.
        var windowStart = PostgresFixture.SeedEpoch.AddDays(-30);
```

Replace the XML doc block above the test with:

```csharp
    /// <summary>
    /// The four tiles agree with the seeded orders, recomputed from the database rather than
    /// hardcoded.
    /// </summary>
    /// <remarks>
    /// This used to anchor on <c>DateTime.UtcNow</c> because the service read the wall clock, and
    /// that made it a test with an expiry date: the seeded orders span <c>SeedEpoch - 1..36
    /// days</c>, so around 2026-09-06 the window would have contained nothing and the
    /// <c>spend &gt; 0</c> assertion below would have failed on a service that was working
    /// correctly. It anchors on <see cref="PostgresFixture.SeedEpoch"/> now because
    /// <c>IDashboardClock</c> resolves the same instant, so both sides of the comparison move
    /// together or not at all.
    /// </remarks>
```

Leave the `Assert.True(stats["spend"] > 0m, ...)` assertion exactly as it is — it is the assertion
the anchor change exists to keep meaningful.

- [ ] **Step 7: Re-anchor `OverviewStatsAveragePriceUsesTheLatestObservedMonth`**

This one currently passes, and that is precisely why it needs changing: its expectation is computed
from the same drifting window as the service, so once the window empties it asserts `0m == 0m` and
stays green while verifying nothing.

Replace `var now = DateTime.UtcNow;` and the `lookbackStart` line below it with:

```csharp
        // Anchored on the epoch for the same reason as the test above, but this one failed more
        // quietly: once the wall-clock lookback stopped matching any observation, `expected`
        // computed to 0m and the assertion became 0m == 0m — a green test verifying nothing.
        var now = PostgresFixture.SeedEpoch;
        var lookbackStart = new DateOnly(now.Year, now.Month, 1).AddMonths(-2);
```

- [ ] **Step 8: Run the full backend suite**

Run: `dotnet test backend/AniKo_API.Tests`
Expected: PASS, 411 tests, 0 warnings.

- [ ] **Step 9: Commit**

```bash
git add backend/AniKo_API/Services backend/AniKo_API.Tests/Services backend/AniKo_API.Tests/Endpoints
git commit -m "refactor(backend): anchor stats and trends windows on IDashboardClock

Both services took TimeProvider and computed trailing windows from it.
Neither needs the wall clock; both need to know when things last
happened. Every frozen instant and asserted date in the unit tests is
unchanged, so the existing assertions keep their meaning.

The two integration tests are re-anchored on SeedEpoch in the same
commit, because they must be: one computed its expectation from
DateTime.UtcNow and fails the moment the service stops doing the same.
The other passed, which was worse — its expectation drifted with the
service, so an empty window would have made it assert 0m == 0m.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: Mutation-check the re-anchored integration tests

**Files:** none permanently. This task temporarily edits
`backend/AniKo_API/Services/DashboardClock.cs` and reverts it.

**Interfaces:**
- Consumes: the re-anchored tests and the wired services from Task 2.
- Produces: evidence, not code.

**Why this is a task at all.** Task 2 left both integration tests green. Green is not the claim
worth making — the claim is that they would go *red* if the clock regressed. Those are different
properties, and this repo has already shipped three defects that lived behind assertions which had
quietly stopped measuring their subject: a `dataStore` field asserted merely non-blank while it
read `"None (skeleton)"` for three phases, a `ThrowOnBadRequest` default no test could observe
because the whole suite booted one environment, and
`OverviewStatsAveragePriceUsesTheLatestObservedMonth` itself, which was on course to assert
`0m == 0m`. A regression test for a calendar bug that itself degrades with the calendar is worth
less than no test, because it also consumes the attention that would have found the problem.

**Requires Docker.** Verified available: Docker 29.6.2, and both tests execute in ~850ms against a
warm daemon with 0 skipped.

- [ ] **Step 1: Mutation-check — break the clock and confirm both tests fail**

Temporarily edit `backend/AniKo_API/Services/DashboardClock.cs`, replacing the body of
`ReferenceNowAsync` with the pre-fix behaviour:

```csharp
        return timeProvider.GetUtcNow().UtcDateTime;
```

Then run: `dotnet test backend/AniKo_API.Tests --filter "FullyQualifiedName~OverviewStatsFiguresAgreeWithTheSeededOrders|FullyQualifiedName~OverviewStatsAveragePriceUsesTheLatestObservedMonth"`

Expected: **BOTH FAIL.** `OverviewStatsFiguresAgreeWithTheSeededOrders` fails because today's
wall clock is 2026-08-16 and the epoch-anchored expectation now disagrees with the wall-clock
figure. `OverviewStatsAveragePriceUsesTheLatestObservedMonth` fails for the same reason.

**If either test still passes, stop and report it.** A passing test here means the assertion is
not measuring the clock and the whole task has produced nothing — which is exactly the failure
mode this codebase has already shipped three times.

- [ ] **Step 2: Restore the clock**

Revert the mutation:

```bash
git checkout backend/AniKo_API/Services/DashboardClock.cs
```

- [ ] **Step 3: Run the full backend suite**

Run: `dotnet test backend/AniKo_API.Tests`
Expected: PASS, 411 tests, 0 warnings.

- [ ] **Step 4: Report — no commit**

This task produces no commit. `git status` must be **clean** at the end: the only file it touched
was reverted in Step 2. If `DashboardClock.cs` still shows as modified, the mutation was not undone
and the next task would deploy a deliberately broken clock.

Report which tests failed under the mutation and with what messages. A test that stayed green is
the finding — it means that assertion is not measuring the clock, and the anchor change bought
nothing.

---

## Task 4: Deploy Part A and verify it live

**Files:** none. This task pushes and verifies.

**Interfaces:**
- Consumes: the deployed service at `https://aniko-api-4emi.onrender.com`.
- Produces: a confirmed-live backend that Part B's success criteria depend on.

- [ ] **Step 1: Push**

```bash
git push origin main
git rev-parse --short HEAD
```

Note the short SHA. Render auto-deploys from `main`.

- [ ] **Step 2: Wait for the deploy, then verify the commit actually shipped**

```bash
curl -s https://aniko-api-4emi.onrender.com/ | python -m json.tool
```

Expected: the `commit` field equals the short SHA from Step 1.

**If it does not match, the deploy is stale.** This has happened before with every other signal
reporting healthy. Trigger a redeploy of the same commit with `clearCache: true` via the Render
MCP tools (service `srv-da0c3utbedkc73aedhqg`, workspace `tea-da09buegekts739dlb7g`) and re-check.
Do not proceed to Step 3 until the SHA matches.

- [ ] **Step 3: Verify the tiles report non-zero values**

```bash
curl -s "https://aniko-api-4emi.onrender.com/api/v1/buyer/overview/stats" | python -m json.tool
```

Expected: four stats with keys `activeOrders`, `spend`, `suppliers`, `avgPrice`, and **`spend`
strictly greater than 0**. Before this task's changes, `spend` was on course to be `0` from
2026-09-06.

- [ ] **Step 4: Verify the trends window sits on the seeded months**

```bash
curl -s "https://aniko-api-4emi.onrender.com/api/v1/pricing/trends?months=12" | python -m json.tool
```

Expected: 12 points, the last dated `2026-08-01`, and **no point where all three crop prices are
`0`**. A trailing run of all-zero points would mean the window is still anchored ahead of the data.

- [ ] **Step 5: Record the result**

No commit. Report the SHA, the `spend` value, and the first and last trend dates.

---

## Task 5: Verify CORS with a real preflight

**Files:** none. This is a gate, and it is deliberately before any fetch code.

**Interfaces:**
- Consumes: the deployed API from Task 4.
- Produces: evidence that the browser will actually be allowed to make these calls.

**Why:** `appsettings.json` lists `https://aniko-agri.netlify.app` and
`appsettings.Development.json` adds `http://localhost:5173`. Reading that configuration is not
evidence that the deployed instance is running it. This project has already spent an hour on a
deployment whose every signal said it was fine.

- [ ] **Step 1: Send a preflight from the Netlify origin**

```bash
curl -s -i -X OPTIONS "https://aniko-api-4emi.onrender.com/api/v1/buyer/overview/stats" \
  -H "Origin: https://aniko-agri.netlify.app" \
  -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: accept"
```

Expected: `HTTP/2 204`, and headers including
`access-control-allow-origin: https://aniko-agri.netlify.app` and
`access-control-allow-methods` containing `GET`.

**A `200` with no `access-control-allow-origin` header is a failure**, not a pass — the browser
reads the header, not the status.

- [ ] **Step 2: Send an actual GET from the Netlify origin**

```bash
curl -s -i "https://aniko-api-4emi.onrender.com/api/v1/orders/recent?limit=3" \
  -H "Origin: https://aniko-agri.netlify.app" | head -20
```

Expected: `HTTP/2 200` and `access-control-allow-origin: https://aniko-agri.netlify.app`.

- [ ] **Step 3: Confirm a foreign origin is refused**

```bash
curl -s -i -X OPTIONS "https://aniko-api-4emi.onrender.com/api/v1/orders/recent" \
  -H "Origin: https://example.com" \
  -H "Access-Control-Request-Method: GET" | head -20
```

Expected: **no** `access-control-allow-origin` header. If this returns `*` or echoes
`https://example.com`, the policy is not doing its job and `AllowAnyOrigin` has crept in
somewhere — stop and report.

- [ ] **Step 4: Report**

No commit. Report the three results. **If Step 1 or Step 2 failed, stop the plan here** and fix
`Cors:AllowedOrigins` before writing any frontend fetch code — every subsequent task's manual
verification depends on this working.

---

## Task 6: API base URL configuration

**Files:**
- Create: `frontend/src/vite-env.d.ts`
- Create: `frontend/.env.example`
- Create: `frontend/src/lib/api/config.ts`
- Test: `frontend/src/lib/api/config.test.ts`

**Interfaces:**
- Consumes: `import.meta.env.VITE_API_BASE_URL`, `import.meta.env.PROD`.
- Produces: `resolveBaseUrl(raw: string | undefined, isProduction: boolean): string` and the module constant `API_BASE_URL: string`.

Note: there is currently **no** `src/vite-env.d.ts` in this repo, despite `tsconfig.app.json`
including `"vite/client"` in `types`. It must be created.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/lib/api/config.test.ts`:

```ts
import { describe, expect, it } from 'vitest'
import { resolveBaseUrl } from './config'

describe('resolveBaseUrl', () => {
  it('uses the configured value', () => {
    expect(resolveBaseUrl('https://aniko-api-4emi.onrender.com', true)).toBe(
      'https://aniko-api-4emi.onrender.com',
    )
  })

  it('strips a trailing slash so paths cannot double up', () => {
    // `${base}${path}` with a trailing slash yields https://host//api/v1/... , which some
    // proxies normalise and some 404. Stripping here means the call sites never think about it.
    expect(resolveBaseUrl('https://aniko-api-4emi.onrender.com/', true)).toBe(
      'https://aniko-api-4emi.onrender.com',
    )
  })

  it('strips several trailing slashes', () => {
    expect(resolveBaseUrl('http://localhost:5199///', false)).toBe('http://localhost:5199')
  })

  it('falls back to the local API in development', () => {
    expect(resolveBaseUrl(undefined, false)).toBe('http://localhost:5199')
    expect(resolveBaseUrl('', false)).toBe('http://localhost:5199')
  })

  it('throws in production when unset', () => {
    // Silence here would be far more expensive than a crash. An empty base makes every request
    // same-origin, Netlify answers the SPA catch-all with index.html at 200, and the app reports
    // a malformed-response error that points nowhere near a missing environment variable.
    expect(() => resolveBaseUrl(undefined, true)).toThrow(/VITE_API_BASE_URL/)
    expect(() => resolveBaseUrl('   ', true)).toThrow(/VITE_API_BASE_URL/)
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/lib/api/config.test.ts`
Expected: FAIL — cannot resolve `./config`.

- [ ] **Step 3: Create the env type declaration**

Create `frontend/src/vite-env.d.ts`:

```ts
/// <reference types="vite/client" />

interface ImportMetaEnv {
  /**
   * Origin of the AniKo API, with no trailing slash and no path.
   *
   * Unset in development, where it falls back to the local API. REQUIRED in a production
   * build — see `src/lib/api/config.ts` for why an unset value throws rather than defaulting
   * to same-origin.
   */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
```

- [ ] **Step 4: Create `.env.example`**

Create `frontend/.env.example`:

```
# Origin of the AniKo API. No trailing slash, no path.
#
# Unset locally, the app targets http://localhost:5199 — so running the backend with
# `dotnet run --project backend/AniKo_API` needs no .env file at all.
#
# In a production build this is REQUIRED and the app throws at load if it is missing.
# On Netlify it is set in the site environment, not in a committed file.
VITE_API_BASE_URL=https://aniko-api-4emi.onrender.com
```

- [ ] **Step 5: Write the config module**

Create `frontend/src/lib/api/config.ts`:

```ts
/** Where the API lives when nothing says otherwise. Matches the backend's launch profile. */
const LOCAL_API = 'http://localhost:5199'

/**
 * Resolves the API origin, and refuses to guess in production.
 *
 * Exported separately from `API_BASE_URL` so it is testable without a module registry reset:
 * `import.meta.env` is frozen at module evaluation, so a constant cannot be re-derived per test.
 */
export function resolveBaseUrl(raw: string | undefined, isProduction: boolean): string {
  const trimmed = raw?.trim() ?? ''

  if (trimmed === '') {
    if (isProduction) {
      /*
       * Throwing beats defaulting to same-origin, and the reason is diagnostic rather than
       * philosophical. With an empty base every request goes to the Netlify origin, where the
       * SPA catch-all in netlify.toml answers `/api/v1/...` with index.html at status 200. The
       * client then fails to parse HTML as JSON and reports a malformed response — an error
       * describing the symptom, pointing at the wrong host, and never mentioning the missing
       * variable that caused it.
       */
      throw new Error(
        'VITE_API_BASE_URL is not set. A production build cannot guess the API origin — ' +
          'set it in the Netlify site environment.',
      )
    }

    return LOCAL_API
  }

  return trimmed.replace(/\/+$/, '')
}

export const API_BASE_URL = resolveBaseUrl(
  import.meta.env.VITE_API_BASE_URL,
  import.meta.env.PROD,
)
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `cd frontend && npx vitest run src/lib/api/config.test.ts`
Expected: PASS, 5 tests.

- [ ] **Step 7: Typecheck**

Run: `cd frontend && npm run typecheck`
Expected: clean.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/vite-env.d.ts frontend/.env.example frontend/src/lib/api
git commit -m "feat(frontend): resolve the API base URL from the environment

Throws in a production build when unset rather than defaulting to
same-origin, where Netlify's SPA catch-all would answer /api/v1 with
index.html at 200 and the failure would surface as a parse error
pointing at the wrong host.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: The transport layer and the failure taxonomy

**Files:**
- Create: `frontend/src/lib/api/failures.ts`
- Create: `frontend/src/lib/api/client.ts`
- Create: `frontend/src/lib/api/testing.ts`
- Test: `frontend/src/lib/api/client.test.ts`

**Interfaces:**
- Consumes: `API_BASE_URL` from `./config`.
- Produces:
  - `type ApiFailure` — the five-member union.
  - `interface Problem { type?: string; title?: string; status?: number; detail?: string; errors?: Record<string, string[]> }`
  - `class ApiError extends Error { readonly failure: ApiFailure }`
  - `isRetryable(failure: ApiFailure): boolean`
  - `apiFetch<T>(path: string, options?: { query?: QueryParams; signal?: AbortSignal; timeoutMs?: number }): Promise<T>`
  - `type QueryParams = Record<string, string | number | boolean | undefined>`
  - `installFetchStub(responses: StubResponse[]): { calls: string[]; restore: () => void }` in `testing.ts`

- [ ] **Step 1: Write the failing test**

Create `frontend/src/lib/api/client.test.ts`:

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiFetch } from './client'
import { ApiError, isRetryable } from './failures'

const OK = { headers: { 'content-type': 'application/json' } }

function stubFetch(impl: typeof fetch) {
  vi.stubGlobal('fetch', impl)
}

afterEach(() => {
  vi.unstubAllGlobals()
})

async function failureOf(promise: Promise<unknown>) {
  const error = await promise.catch((e) => e)
  expect(error).toBeInstanceOf(ApiError)
  return (error as ApiError).failure
}

describe('apiFetch', () => {
  it('returns parsed JSON on success', async () => {
    stubFetch(async () => new Response(JSON.stringify({ stats: [] }), { status: 200, ...OK }))

    expect(await apiFetch('/api/v1/buyer/overview/stats')).toEqual({ stats: [] })
  })

  it('builds the URL from the base, path and query, skipping undefined params', async () => {
    let seen = ''
    stubFetch(async (input) => {
      seen = String(input)
      return new Response('{}', { status: 200, ...OK })
    })

    await apiFetch('/api/v1/orders/recent', { query: { limit: 5, cursor: undefined } })

    expect(seen).toContain('/api/v1/orders/recent?limit=5')
    expect(seen).not.toContain('cursor')
  })

  it('classifies a thrown fetch as network', async () => {
    stubFetch(async () => {
      throw new TypeError('Failed to fetch')
    })

    expect(await failureOf(apiFetch('/x'))).toEqual({ kind: 'network' })
  })

  it('classifies an aborted request as timeout', async () => {
    stubFetch(async () => {
      throw new DOMException('The operation was aborted.', 'TimeoutError')
    })

    expect(await failureOf(apiFetch('/x'))).toEqual({ kind: 'timeout' })
  })

  it('classifies a 500 as server', async () => {
    stubFetch(async () => new Response('boom', { status: 500 }))

    expect(await failureOf(apiFetch('/x'))).toEqual({ kind: 'server', status: 500 })
  })

  it('classifies a 400 as client and parses problem+json', async () => {
    const problem = {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { lng: ['lng is required.'] },
    }
    stubFetch(
      async () =>
        new Response(JSON.stringify(problem), {
          status: 400,
          headers: { 'content-type': 'application/problem+json' },
        }),
    )

    const failure = await failureOf(apiFetch('/api/v1/suppliers/nearby', { query: { lat: 14.6 } }))

    expect(failure).toEqual({ kind: 'client', status: 400, problem })
  })

  it('tolerates a 400 with no body and no content type', async () => {
    /*
     * This is not hypothetical. Before the ThrowOnBadRequest fix, this exact API answered a
     * binding failure in production with a bare 400 — no body, no content type — while answering
     * the same request locally with a full problem document. The server is fixed. A client that
     * threw while trying to explain an error would turn a regression into a blank screen.
     */
    stubFetch(async () => new Response(null, { status: 400 }))

    expect(await failureOf(apiFetch('/x'))).toEqual({ kind: 'client', status: 400 })
  })

  it('classifies unparseable 2xx bodies as malformed', async () => {
    stubFetch(async () => new Response('<!doctype html><html></html>', { status: 200, ...OK }))

    expect(await failureOf(apiFetch('/x'))).toEqual({ kind: 'malformed' })
  })
})

describe('isRetryable', () => {
  it('retries transport and server failures', () => {
    expect(isRetryable({ kind: 'network' })).toBe(true)
    expect(isRetryable({ kind: 'timeout' })).toBe(true)
    expect(isRetryable({ kind: 'server', status: 503 })).toBe(true)
  })

  it('never retries our own bugs', () => {
    // The UI builds these query strings, so a 400 means we constructed one wrong. Retrying
    // burns the deadline and buries the evidence under a generic network message.
    expect(isRetryable({ kind: 'client', status: 400 })).toBe(false)
    expect(isRetryable({ kind: 'malformed' })).toBe(false)
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/lib/api/client.test.ts`
Expected: FAIL — cannot resolve `./client` or `./failures`.

- [ ] **Step 3: Write the failure taxonomy**

Create `frontend/src/lib/api/failures.ts`:

```ts
/** An RFC 9457 problem document, every field optional because a server may omit any of them. */
export interface Problem {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

/**
 * Why a request did not produce data.
 *
 * The union exists because the retry policy falls straight out of it, and because the four
 * cases need genuinely different words on screen. Collapsing them into one "something went
 * wrong" would offer a Retry button for a malformed URL we built ourselves, and withhold one
 * from a user whose train just went into a tunnel.
 */
export type ApiFailure =
  /** fetch itself threw: offline, DNS, a refused connection, or a CORS rejection. */
  | { kind: 'network' }
  /** The attempt exceeded its budget. On this deployment, usually a cold start. */
  | { kind: 'timeout' }
  /** 4xx. We built the request. This is our bug. */
  | { kind: 'client'; status: number; problem?: Problem }
  /** 5xx. Their bug, and nothing the reader can do about it. */
  | { kind: 'server'; status: number }
  /** 2xx whose body is not the JSON we expect. A contract break. */
  | { kind: 'malformed' }

export class ApiError extends Error {
  readonly failure: ApiFailure

  constructor(failure: ApiFailure) {
    super(`API request failed: ${failure.kind}`)
    this.name = 'ApiError'
    this.failure = failure
  }
}

/**
 * Whether trying again could plausibly succeed.
 *
 * `client` and `malformed` are excluded on purpose. Both mean the request or the contract is
 * wrong, and neither is fixed by repetition — retrying them spends the deadline and then reports
 * a timeout, which hides the real, actionable error behind a misleading one.
 */
export function isRetryable(failure: ApiFailure): boolean {
  return failure.kind === 'network' || failure.kind === 'timeout' || failure.kind === 'server'
}
```

- [ ] **Step 4: Write the client**

Create `frontend/src/lib/api/client.ts`:

```ts
import { API_BASE_URL } from './config'
import { ApiError, type Problem } from './failures'

export type QueryParams = Record<string, string | number | boolean | undefined>

/**
 * How long one attempt may take.
 *
 * 30 seconds, sized on a measured cold start of 22.4s on the free Render instance (0.036s to
 * connect, 22.391s to first byte; warm is 0.096s — a 233x difference). That is roughly 35%
 * headroom. It is a judgement call from ONE measurement: if this deployment moves off the free
 * tier, or the startup migration grows, re-measure rather than adjusting by feel.
 */
export const ATTEMPT_TIMEOUT_MS = 30_000

function buildUrl(path: string, query?: QueryParams): string {
  const url = new URL(`${API_BASE_URL}${path}`)

  for (const [key, value] of Object.entries(query ?? {})) {
    // Undefined means "not specified", which must not become the string "undefined" in the query
    // string — the backend validates these and would answer 400 for a parameter we never meant
    // to send.
    if (value !== undefined) {
      url.searchParams.set(key, String(value))
    }
  }

  return url.toString()
}

async function readProblem(response: Response): Promise<Problem | undefined> {
  /*
   * Every step here can fail, and all of them must fail quietly. Before the ThrowOnBadRequest
   * fix this API answered production binding failures with a bare 400 — no body, no content
   * type. A client that threw while parsing an error document would replace a legible "you
   * forgot lng" with an unhandled rejection.
   */
  try {
    const contentType = response.headers.get('content-type') ?? ''

    if (!contentType.includes('json')) {
      return undefined
    }

    const body = (await response.json()) as unknown

    return body !== null && typeof body === 'object' ? (body as Problem) : undefined
  } catch {
    return undefined
  }
}

/**
 * One HTTP attempt against the API, with every failure mode named.
 *
 * Throws `ApiError` carrying an `ApiFailure` — never returns a partial result. Retry policy and
 * backoff live one layer up in `useApiResource`, because they are a UI concern: how long a person
 * will wait is not a property of the transport.
 */
export async function apiFetch<T>(
  path: string,
  options: { query?: QueryParams; signal?: AbortSignal; timeoutMs?: number } = {},
): Promise<T> {
  const { query, signal, timeoutMs = ATTEMPT_TIMEOUT_MS } = options

  // Composed rather than chosen: the caller's signal aborts on unmount, the timeout aborts a
  // hung request, and either alone would leave the other case unhandled.
  const timeout = AbortSignal.timeout(timeoutMs)
  const composed = signal ? AbortSignal.any([signal, timeout]) : timeout

  let response: Response

  try {
    response = await fetch(buildUrl(path, query), {
      headers: { Accept: 'application/json' },
      signal: composed,
    })
  } catch (error) {
    /*
     * A caller-initiated abort is not a failure to report — the component is gone. It is
     * rethrown as-is so `useApiResource` can recognise and discard it, rather than rendering an
     * error for a panel that has already unmounted.
     */
    if (signal?.aborted) {
      throw error
    }

    const timedOut = error instanceof DOMException && error.name === 'TimeoutError'
    throw new ApiError(timedOut ? { kind: 'timeout' } : { kind: 'network' })
  }

  if (response.status >= 500) {
    throw new ApiError({ kind: 'server', status: response.status })
  }

  if (!response.ok) {
    const problem = await readProblem(response)

    // `problem` is omitted rather than set to undefined so the failure object compares equal to
    // a literal without one, which keeps the tests honest about what was actually parsed.
    throw new ApiError(
      problem
        ? { kind: 'client', status: response.status, problem }
        : { kind: 'client', status: response.status },
    )
  }

  try {
    return (await response.json()) as T
  } catch {
    throw new ApiError({ kind: 'malformed' })
  }
}
```

- [ ] **Step 5: Write the fetch stub helper**

Create `frontend/src/lib/api/testing.ts`:

```ts
import { vi } from 'vitest'

/**
 * A queue-backed `fetch` stub, in place of MSW.
 *
 * MSW is the better tool for a large API surface. This app has five read-only endpoints and
 * chose a hand-rolled client precisely to avoid dependencies whose value is elsewhere; a stub
 * that answers from a queue is enough to drive every branch of that client.
 */
export type StubStep =
  | { ok: true; body: unknown; status?: number; contentType?: string }
  | { ok: false; throws: unknown }

export interface FetchStub {
  /** Every URL requested, in order, so a test can assert what was actually asked for. */
  readonly calls: string[]
  restore: () => void
}

export function installFetchStub(steps: StubStep[]): FetchStub {
  const calls: string[] = []
  let index = 0

  vi.stubGlobal('fetch', async (input: RequestInfo | URL) => {
    calls.push(String(input))

    // The last step repeats rather than running out. A retry test that exhausts the queue would
    // fail with "undefined is not a function" instead of the assertion it was written for.
    const step = steps[Math.min(index, steps.length - 1)]
    index += 1

    if (!step.ok) {
      throw step.throws
    }

    return new Response(step.body === null ? null : JSON.stringify(step.body), {
      status: step.status ?? 200,
      headers: { 'content-type': step.contentType ?? 'application/json' },
    })
  })

  return { calls, restore: () => vi.unstubAllGlobals() }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `cd frontend && npx vitest run src/lib/api/client.test.ts`
Expected: PASS, 10 tests.

- [ ] **Step 7: Typecheck and lint**

Run: `cd frontend && npm run typecheck && npm run lint`
Expected: clean.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/lib/api
git commit -m "feat(frontend): add apiFetch and the failure taxonomy

Five named failure kinds, because the retry policy falls out of them:
4xx and malformed responses are our own bugs and are never retried,
since repetition cannot fix a URL we built wrong and spending the
deadline on it reports a timeout instead of the real error.

Tolerates a 400 with no body and no content type — the shape this API
served in production before the ThrowOnBadRequest fix.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 8: `useApiResource`

**Files:**
- Create: `frontend/src/lib/api/useApiResource.ts`
- Test: `frontend/src/lib/api/useApiResource.test.ts`

**Interfaces:**
- Consumes: `apiFetch`, `QueryParams` from `./client`; `ApiError`, `ApiFailure`, `isRetryable` from `./failures`; `installFetchStub` from `./testing`.
- Produces: `type ApiResource<T>` and `useApiResource<T>(path: string, options: { query?: QueryParams; adapt: (raw: unknown) => T }): ApiResource<T>`.

**The identity trap, and how this design avoids it.** `adapt` is written inline at every call
site, so it is a new function on every render. If the effect depended on it the hook would refetch
forever. It is therefore held in a ref and deliberately excluded from the dependency array, while
the effect keys on `path` and a serialised `query`. An executor who "fixes" the lint warning by
adding `adapt` to the deps will produce an infinite request loop against a service with a 22-second
cold start.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/lib/api/useApiResource.test.ts`:

```ts
import { renderHook, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from './failures'
import { installFetchStub } from './testing'
import { useApiResource, WAKING_AFTER_MS } from './useApiResource'

const identity = (raw: unknown) => raw

afterEach(() => {
  vi.unstubAllGlobals()
  vi.useRealTimers()
})

describe('useApiResource', () => {
  it('starts loading and resolves to success', async () => {
    installFetchStub([{ ok: true, body: { value: 1 } }])

    const { result } = renderHook(() => useApiResource('/x', { adapt: identity }))

    expect(result.current.status).toBe('loading')
    await waitFor(() => expect(result.current.status).toBe('success'))
    expect(result.current).toMatchObject({ status: 'success', data: { value: 1 } })
  })

  it('runs the adapter on the raw body', async () => {
    installFetchStub([{ ok: true, body: { n: 2 } }])

    const { result } = renderHook(() =>
      useApiResource('/x', { adapt: (raw) => (raw as { n: number }).n * 21 }),
    )

    await waitFor(() => expect(result.current).toMatchObject({ status: 'success', data: 42 }))
  })

  it('does not flag waking before the threshold', async () => {
    installFetchStub([{ ok: true, body: {} }])

    const { result } = renderHook(() => useApiResource('/x', { adapt: identity }))

    // A warm response arrives in ~0.1s and must never show the message; claiming the server is
    // asleep while it answers instantly is worse than saying nothing.
    expect(result.current).toMatchObject({ status: 'loading', waking: false })
  })

  it('flags waking once the threshold passes', async () => {
    vi.useFakeTimers()
    installFetchStub([{ ok: false, throws: new Promise(() => {}) }])
    vi.stubGlobal('fetch', () => new Promise(() => {}))

    const { result } = renderHook(() => useApiResource('/x', { adapt: identity }))

    await vi.advanceTimersByTimeAsync(WAKING_AFTER_MS + 10)

    expect(result.current).toMatchObject({ status: 'loading', waking: true })
  })

  it('retries a server failure and succeeds', async () => {
    vi.useFakeTimers()
    const stub = installFetchStub([
      { ok: true, body: {}, status: 503 },
      { ok: true, body: { value: 'second' } },
    ])

    const { result } = renderHook(() => useApiResource('/x', { adapt: identity }))

    await vi.advanceTimersByTimeAsync(2_000)

    expect(result.current).toMatchObject({ status: 'success', data: { value: 'second' } })
    expect(stub.calls).toHaveLength(2)
  })

  it('never retries a 4xx', async () => {
    vi.useFakeTimers()
    const stub = installFetchStub([
      { ok: true, body: { title: 'Bad' }, status: 400, contentType: 'application/problem+json' },
    ])

    const { result } = renderHook(() => useApiResource('/x', { adapt: identity }))

    await vi.advanceTimersByTimeAsync(10_000)

    expect(result.current.status).toBe('error')
    expect(stub.calls).toHaveLength(1)
  })

  it('gives up after the attempt budget and offers a retry', async () => {
    vi.useFakeTimers()
    const stub = installFetchStub([{ ok: false, throws: new TypeError('Failed to fetch') }])

    const { result } = renderHook(() => useApiResource('/x', { adapt: identity }))

    await vi.advanceTimersByTimeAsync(10_000)

    expect(result.current).toMatchObject({ status: 'error', failure: { kind: 'network' } })
    expect(stub.calls).toHaveLength(3)
  })

  it('refetches when retry is called', async () => {
    vi.useFakeTimers()
    const stub = installFetchStub([{ ok: false, throws: new TypeError('Failed to fetch') }])

    const { result } = renderHook(() => useApiResource('/x', { adapt: identity }))
    await vi.advanceTimersByTimeAsync(10_000)

    expect(result.current.status).toBe('error')
    const before = stub.calls.length

    if (result.current.status === 'error') {
      result.current.retry()
    }
    await vi.advanceTimersByTimeAsync(10_000)

    expect(stub.calls.length).toBeGreaterThan(before)
  })

  it('refetches when the query changes', async () => {
    const stub = installFetchStub([{ ok: true, body: {} }])

    const { rerender, result } = renderHook(
      ({ months }) => useApiResource('/trends', { query: { months }, adapt: identity }),
      { initialProps: { months: 6 } },
    )

    await waitFor(() => expect(result.current.status).toBe('success'))
    rerender({ months: 12 })
    await waitFor(() => expect(stub.calls).toHaveLength(2))

    expect(stub.calls[1]).toContain('months=12')
  })

  it('does not refetch when only the adapter identity changes', async () => {
    // The adapter is written inline at every call site, so it is a new function every render.
    // Depending on it would mean an unbounded request loop against a 22-second cold start.
    const stub = installFetchStub([{ ok: true, body: {} }])

    const { rerender, result } = renderHook(() =>
      useApiResource('/x', { adapt: (raw) => raw }),
    )

    await waitFor(() => expect(result.current.status).toBe('success'))
    rerender()
    rerender()

    expect(stub.calls).toHaveLength(1)
  })

  it('ignores a resolution that lands after unmount', async () => {
    const errors = vi.spyOn(console, 'error').mockImplementation(() => {})
    installFetchStub([{ ok: true, body: {} }])

    const { unmount } = renderHook(() => useApiResource('/x', { adapt: identity }))
    unmount()
    await Promise.resolve()

    // React logs a warning on a state update after unmount; none should appear.
    expect(errors).not.toHaveBeenCalled()
    errors.mockRestore()
  })
})

describe('ApiError', () => {
  it('carries the failure it was constructed with', () => {
    expect(new ApiError({ kind: 'network' }).failure).toEqual({ kind: 'network' })
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/lib/api/useApiResource.test.ts`
Expected: FAIL — cannot resolve `./useApiResource`.

- [ ] **Step 3: Write the hook**

Create `frontend/src/lib/api/useApiResource.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react'
import { apiFetch, type QueryParams } from './client'
import { ApiError, isRetryable, type ApiFailure } from './failures'

/**
 * When the honest "we are waking the server" message appears.
 *
 * 3 seconds. Below it, a plain skeleton is the truthful answer — a warm response takes 0.096s
 * and would flash a message about a server that never slept. Above it, silence starts to read as
 * breakage.
 */
export const WAKING_AFTER_MS = 3_000

/**
 * The ceiling across every attempt, backoff included.
 *
 * 45 seconds, against a measured 22.4s cold start. This is NOT the per-attempt timeout (30s, in
 * client.ts) and the two must not be conflated: three 30s attempts plus backoff would leave a
 * reader watching a skeleton for 97 seconds. Past 45s, an error with a Retry button is a better
 * offer than more waiting.
 */
export const DEADLINE_MS = 45_000

const MAX_ATTEMPTS = 3

/** Backoff before attempt 2 and attempt 3. Short, because the deadline is the real constraint. */
const BACKOFF_MS = [1_000, 2_000]

export type ApiResource<T> =
  | { status: 'loading'; waking: boolean }
  | { status: 'success'; data: T }
  | { status: 'error'; failure: ApiFailure; retry: () => void }

const sleep = (ms: number, signal: AbortSignal) =>
  new Promise<void>((resolve) => {
    const timer = setTimeout(resolve, ms)
    signal.addEventListener('abort', () => {
      clearTimeout(timer)
      resolve()
    })
  })

/**
 * Fetches one endpoint and reports it as a state machine.
 *
 * One resource per panel, no shared cache: the five panels are independent, and a cache would be
 * infrastructure serving a requirement this app does not have.
 */
export function useApiResource<T>(
  path: string,
  options: { query?: QueryParams; adapt: (raw: unknown) => T },
): ApiResource<T> {
  const { query, adapt } = options

  /*
   * The adapter is held in a ref and deliberately kept OUT of the effect's dependencies. Call
   * sites write it inline, so it has a new identity on every render; depending on it would
   * refetch forever. Do not "fix" this by adding `adapt` to the array below.
   */
  const adaptRef = useRef(adapt)
  adaptRef.current = adapt

  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<ApiResource<T>>({ status: 'loading', waking: false })

  const retry = useCallback(() => {
    setState({ status: 'loading', waking: false })
    setAttempt((n) => n + 1)
  }, [])

  // Serialised so the effect keys on the query's VALUE, not its object identity. `{ months: 6 }`
  // is a new object every render and would otherwise refetch on every parent update.
  const queryKey = JSON.stringify(query ?? {})

  useEffect(() => {
    const controller = new AbortController()
    const startedAt = Date.now()

    const wakingTimer = setTimeout(() => {
      setState((current) =>
        current.status === 'loading' ? { status: 'loading', waking: true } : current,
      )
    }, WAKING_AFTER_MS)

    const run = async () => {
      let lastFailure: ApiFailure = { kind: 'network' }

      for (let n = 0; n < MAX_ATTEMPTS; n += 1) {
        const remaining = DEADLINE_MS - (Date.now() - startedAt)

        if (remaining <= 0) {
          break
        }

        try {
          const raw = await apiFetch<unknown>(path, {
            query: JSON.parse(queryKey) as QueryParams,
            signal: controller.signal,
            // The deadline governs. A later attempt gets what is left of it, not a fresh budget.
            timeoutMs: remaining,
          })

          if (!controller.signal.aborted) {
            setState({ status: 'success', data: adaptRef.current(raw) })
          }

          return
        } catch (error) {
          // The component is gone; there is nothing to tell and no state to set.
          if (controller.signal.aborted) {
            return
          }

          // A non-ApiError escaping apiFetch means the adapter threw, or something else did.
          // Reported as malformed rather than swallowed: a shape we cannot map is a contract
          // break, and it must not be retried.
          lastFailure = error instanceof ApiError ? error.failure : { kind: 'malformed' }

          if (!isRetryable(lastFailure) || n === MAX_ATTEMPTS - 1) {
            break
          }

          await sleep(BACKOFF_MS[n] ?? 0, controller.signal)
        }
      }

      if (!controller.signal.aborted) {
        setState({ status: 'error', failure: lastFailure, retry })
      }
    }

    void run()

    return () => {
      clearTimeout(wakingTimer)
      controller.abort()
    }
  }, [path, queryKey, attempt, retry])

  return state
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd frontend && npx vitest run src/lib/api/useApiResource.test.ts`
Expected: PASS, 12 tests.

- [ ] **Step 5: Typecheck and lint**

Run: `cd frontend && npm run typecheck && npm run lint`
Expected: clean.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/lib/api
git commit -m "feat(frontend): add useApiResource with cold-start-aware retry

Two budgets that must not be conflated: 30s per attempt, 45s overall.
Three 30s attempts plus backoff would be 97 seconds of skeleton.

The waking flag trips at 3s so a warm 0.096s response never claims the
server was asleep. The adapter is held in a ref and excluded from the
effect deps — call sites write it inline, so depending on it would loop
forever against a 22-second cold start.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 9: Panel state copy and the shared `PanelState` component

**Files:**
- Modify: `frontend/src/app/locales/en.json`, `frontend/src/app/locales/fil.json`
- Create: `frontend/src/components/PanelState.tsx`
- Test: `frontend/src/components/PanelState.test.tsx`

**Interfaces:**
- Consumes: `ApiResource` from `@/lib/api/useApiResource`; `isRetryable` from `@/lib/api/failures`.
- Produces: `PanelState({ resource, title, skeleton }: { resource: ApiResource<unknown>; title: string; skeleton: ReactNode }): ReactNode`.

- [ ] **Step 1: Add the copy to `en.json`**

In `frontend/src/app/locales/en.json`, add these five entries after the `"session.role_farmer"` line:

```json
  "panel.waking": "Waking the server. This can take up to 30 seconds on the free tier.",
  "panel.error_title": "Couldn't load this section",
  "panel.error_retryable": "The connection didn't get through. Try again.",
  "panel.error_permanent": "Something is wrong on our side. This one isn't yours to fix.",
  "panel.retry": "Try again",
```

- [ ] **Step 2: Add the same five keys to `fil.json`**

In `frontend/src/app/locales/fil.json`, add after `"session.role_farmer"`:

```json
  "panel.waking": "Ginigising ang server. Maaaring umabot ito ng 30 segundo sa libreng tier.",
  "panel.error_title": "Hindi ma-load ang bahaging ito",
  "panel.error_retryable": "Hindi nakarating ang koneksyon. Subukan muli.",
  "panel.error_permanent": "May mali sa aming panig. Hindi ito sa iyo.",
  "panel.retry": "Subukan muli",
```

- [ ] **Step 3: Verify locale parity holds**

Run: `cd frontend && npx vitest run src/app/locales/locales.test.ts`
Expected: PASS. If it fails, one file has a key the other does not — fix before continuing.

- [ ] **Step 4: Write the failing component test**

Create `frontend/src/components/PanelState.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ApiResource } from '@/lib/api/useApiResource'
import { PanelState } from './PanelState'

const skeleton = <div data-testid="skeleton" />

function renderState(resource: ApiResource<unknown>) {
  return render(<PanelState resource={resource} title="Recent Orders" skeleton={skeleton} />)
}

describe('PanelState', () => {
  it('renders the skeleton while loading', () => {
    renderState({ status: 'loading', waking: false })

    expect(screen.getByTestId('skeleton')).toBeInTheDocument()
    expect(screen.queryByText(/waking the server/i)).not.toBeInTheDocument()
  })

  it('adds the waking message without removing the skeleton', () => {
    // The skeleton stays because the layout must not jump when the message appears — this state
    // is on screen for twenty seconds on a cold start, not for a frame.
    renderState({ status: 'loading', waking: true })

    expect(screen.getByTestId('skeleton')).toBeInTheDocument()
    expect(screen.getByText(/waking the server/i)).toBeInTheDocument()
  })

  it('announces the waking message politely', () => {
    renderState({ status: 'loading', waking: true })

    const status = screen.getByRole('status')
    expect(status).toHaveAttribute('aria-live', 'polite')
  })

  it('offers a retry button for a retryable failure', async () => {
    const retry = vi.fn()
    renderState({ status: 'error', failure: { kind: 'network' }, retry })

    const button = screen.getByRole('button', { name: /try again/i })
    await userEvent.click(button)

    expect(retry).toHaveBeenCalledOnce()
  })

  it('offers no retry button for a 4xx, because there is nothing to retry', () => {
    renderState({
      status: 'error',
      failure: { kind: 'client', status: 400 },
      retry: vi.fn(),
    })

    expect(screen.queryByRole('button', { name: /try again/i })).not.toBeInTheDocument()
    expect(screen.getByText(/wrong on our side/i)).toBeInTheDocument()
  })

  it('keeps the panel title visible in an error state', () => {
    // Losing the heading turns a broken panel into an anonymous red box; a reader cannot tell
    // which of five sections failed.
    renderState({ status: 'error', failure: { kind: 'server', status: 500 }, retry: vi.fn() })

    expect(screen.getByRole('heading', { name: 'Recent Orders' })).toBeInTheDocument()
  })

  it('renders nothing for a success resource', () => {
    const { container } = renderState({ status: 'success', data: [] })

    expect(container).toBeEmptyDOMElement()
  })
})
```

- [ ] **Step 5: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/components/PanelState.test.tsx`
Expected: FAIL — cannot resolve `./PanelState`.

- [ ] **Step 6: Write the component**

Create `frontend/src/components/PanelState.tsx`:

```tsx
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { isRetryable } from '@/lib/api/failures'
import type { ApiResource } from '@/lib/api/useApiResource'

export interface PanelStateProps {
  resource: ApiResource<unknown>
  /** The panel's own heading, kept on screen so a failed panel is still identifiable. */
  title: string
  /** The panel's own skeleton. Deliberately not shared — a chart and a map do not look alike. */
  skeleton: ReactNode
}

/**
 * Everything a panel shows when it does not have data.
 *
 * Shared because five panels would otherwise grow five slightly different answers to the same
 * four questions, and the differences would be accidental rather than designed. What is NOT
 * shared is the skeleton: its whole job is to occupy the shape of the content it stands in for.
 *
 * Returns `null` for a successful resource, so a panel can early-return this unconditionally and
 * then render its content.
 */
export function PanelState({ resource, title, skeleton }: PanelStateProps) {
  const { t } = useTranslation()

  if (resource.status === 'success') {
    return null
  }

  if (resource.status === 'loading') {
    return (
      <>
        {skeleton}
        {resource.waking ? (
          <p role="status" aria-live="polite" className="mt-3 text-sm text-muted-fg">
            {t('panel.waking')}
          </p>
        ) : null}
      </>
    )
  }

  const retryable = isRetryable(resource.failure)

  return (
    <div className="rounded-xl bg-surface p-5">
      <h2 className="text-lg font-bold text-primary">{title}</h2>

      <p className="mt-3 text-sm font-semibold text-destructive">{t('panel.error_title')}</p>

      {/*
        Two messages, not one. A reader who can act on a failure and a reader who cannot need
        different words — offering a Retry button for a malformed request we built ourselves
        would invite someone to keep pressing it at a problem no amount of pressing fixes.
      */}
      <p className="mt-1 text-sm text-muted-fg">
        {retryable ? t('panel.error_retryable') : t('panel.error_permanent')}
      </p>

      {/*
        The problem's own title, in development only. It names the offending parameter, which is
        exactly what a developer wants and exactly what a buyer should never be shown.
      */}
      {import.meta.env.DEV && resource.failure.kind === 'client' && resource.failure.problem?.title ? (
        <p className="mt-2 text-xs text-muted-fg">{resource.failure.problem.title}</p>
      ) : null}

      {retryable ? (
        <button
          type="button"
          onClick={resource.retry}
          className="mt-4 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          {t('panel.retry')}
        </button>
      ) : null}
    </div>
  )
}
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `cd frontend && npx vitest run src/components/PanelState.test.tsx`
Expected: PASS, 7 tests.

- [ ] **Step 8: Verify the hex guard still passes**

Run: `cd frontend && npx vitest run src/lib/no-raw-hex.test.ts`
Expected: PASS. `PanelState.tsx` uses only token classes (`text-destructive`, `bg-primary`,
`text-muted-fg`, `bg-surface`), no literals.

- [ ] **Step 9: Commit**

```bash
git add frontend/src/components/PanelState.tsx frontend/src/components/PanelState.test.tsx frontend/src/app/locales
git commit -m "feat(frontend): add PanelState for loading, waking and error

Two error messages rather than one: a reader who can act on a failure
and a reader who cannot need different words, and a Retry button on a
request we built wrong invites pressing at a problem pressing cannot fix.

The heading stays visible in the error state so a failed panel is still
identifiable among five.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 10: The three missing skeletons

**Files:**
- Create: `frontend/src/features/overview/StatTilesSkeleton.tsx`
- Create: `frontend/src/features/lots/FeaturedLotsSkeleton.tsx`
- Create: `frontend/src/features/orders/RecentOrdersSkeleton.tsx`
- Modify: `frontend/src/app/locales/en.json`, `frontend/src/app/locales/fil.json`
- Test: `frontend/src/features/overview/StatTilesSkeleton.test.tsx`

**Interfaces:**
- Consumes: nothing beyond `react-i18next`.
- Produces: `StatTilesSkeleton()`, `FeaturedLotsSkeleton()`, `RecentOrdersSkeleton()` — all zero-prop components.

Each mirrors the geometry of the content it stands in for, following the precedent set by
`MarketPriceTrendsSkeleton`, whose doc comment explains why: a fallback that is the wrong height
lets the rest of the page settle and then shoves it, and on this deployment that state is on
screen for seconds.

- [ ] **Step 1: Add the three loading strings to both locale files**

In `en.json`, add after `"stats.delta_flat"`:

```json
  "stats.loading": "Loading your overview figures…",
```

after `"lots.empty"`:

```json
  "lots.loading": "Loading featured lots…",
```

and after `"orders.empty"` (mind the trailing comma — `orders.empty` is currently the last key):

```json
  "orders.loading": "Loading recent orders…",
```

In `fil.json`, at the matching positions:

```json
  "stats.loading": "Naglo-load ang iyong mga bilang…",
  "lots.loading": "Naglo-load ang mga itinatampok na pakyawan…",
  "orders.loading": "Naglo-load ang mga kamakailang order…",
```

- [ ] **Step 2: Write the failing test**

Create `frontend/src/features/overview/StatTilesSkeleton.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StatTilesSkeleton } from './StatTilesSkeleton'

describe('StatTilesSkeleton', () => {
  it('announces itself politely to assistive technology', () => {
    render(<StatTilesSkeleton />)

    const status = screen.getByRole('status')
    expect(status).toHaveAttribute('aria-live', 'polite')
    expect(screen.getByText(/loading your overview figures/i)).toBeInTheDocument()
  })

  it('renders four placeholders, matching the tile count it stands in for', () => {
    // Four, not three or a generic block. The row is a four-column grid; a placeholder with a
    // different count resizes the grid when the data lands, which is the shift skeletons exist
    // to prevent.
    const { container } = render(<StatTilesSkeleton />)

    expect(container.querySelectorAll('[data-slot="stat-skeleton"]')).toHaveLength(4)
  })
})
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/features/overview/StatTilesSkeleton.test.tsx`
Expected: FAIL — cannot resolve `./StatTilesSkeleton`.

- [ ] **Step 4: Write `StatTilesSkeleton`**

Create `frontend/src/features/overview/StatTilesSkeleton.tsx`:

```tsx
import { useTranslation } from 'react-i18next'

/**
 * Placeholder for the four stat tiles.
 *
 * The grid classes below are copied from `StatTilesRow`, not approximated. A skeleton that
 * breaks to two columns at a different width than the content it replaces produces a visible
 * reflow at exactly the moment the reader's eye is on the tiles.
 */
export function StatTilesSkeleton() {
  const { t } = useTranslation()

  return (
    <div role="status" aria-live="polite">
      <span className="sr-only">{t('stats.loading')}</span>

      <div
        aria-hidden="true"
        className="grid animate-pulse grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4"
      >
        {[0, 1, 2, 3].map((slot) => (
          <div
            key={slot}
            data-slot="stat-skeleton"
            className="rounded-xl bg-surface p-5"
          >
            <div className="h-4 w-24 rounded bg-muted" />
            <div className="mt-3 h-8 w-32 rounded bg-muted" />
            <div className="mt-3 h-4 w-28 rounded-full bg-muted" />
          </div>
        ))}
      </div>
    </div>
  )
}
```

- [ ] **Step 5: Write `FeaturedLotsSkeleton`**

Create `frontend/src/features/lots/FeaturedLotsSkeleton.tsx`:

```tsx
import { useTranslation } from 'react-i18next'

/**
 * Placeholder for the featured-lots scroller.
 *
 * Three cards, laid out with the scroller's own horizontal flow. The real scroller holds six,
 * but only about three are on screen at any width — a placeholder that rendered all six would
 * make the panel wider than the content ever is.
 */
export function FeaturedLotsSkeleton() {
  const { t } = useTranslation()

  return (
    <div role="status" aria-live="polite" className="rounded-xl bg-surface p-5">
      <span className="sr-only">{t('lots.loading')}</span>

      <div aria-hidden="true" className="animate-pulse">
        <div className="h-6 w-56 rounded bg-muted" />

        <div className="mt-4 flex gap-4 overflow-hidden">
          {[0, 1, 2].map((slot) => (
            <div key={slot} data-slot="lot-skeleton" className="w-64 shrink-0">
              <div className="h-5 w-40 rounded bg-muted" />
              <div className="mt-2 h-4 w-28 rounded-full bg-muted" />
              <div className="mt-4 h-4 w-32 rounded bg-muted" />
              <div className="mt-2 h-4 w-24 rounded bg-muted" />
              <div className="mt-4 h-9 w-full rounded-lg bg-muted" />
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
```

- [ ] **Step 6: Write `RecentOrdersSkeleton`**

Create `frontend/src/features/orders/RecentOrdersSkeleton.tsx`:

```tsx
import { useTranslation } from 'react-i18next'

/**
 * Placeholder for the recent-orders table.
 *
 * Five rows, matching `RecentOrdersPanel`'s default limit of 5, plus a header strip. A table
 * skeleton with the wrong row count is the most visible kind of layout shift on this page,
 * because the panel sits beside the taller lots section and any height change moves that too.
 */
export function RecentOrdersSkeleton() {
  const { t } = useTranslation()

  return (
    <div role="status" aria-live="polite" className="rounded-xl bg-surface p-5">
      <span className="sr-only">{t('orders.loading')}</span>

      <div aria-hidden="true" className="animate-pulse">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="h-6 w-40 rounded bg-muted" />
          <div className="h-4 w-28 rounded bg-muted" />
        </div>

        <div className="mt-4 h-4 w-full rounded bg-muted" />

        {[0, 1, 2, 3, 4].map((slot) => (
          <div key={slot} data-slot="order-skeleton" className="mt-3 h-10 w-full rounded bg-muted" />
        ))}
      </div>
    </div>
  )
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `cd frontend && npx vitest run src/features/overview/StatTilesSkeleton.test.tsx src/app/locales/locales.test.ts src/lib/no-raw-hex.test.ts`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/features/overview/StatTilesSkeleton.tsx frontend/src/features/overview/StatTilesSkeleton.test.tsx frontend/src/features/lots/FeaturedLotsSkeleton.tsx frontend/src/features/orders/RecentOrdersSkeleton.tsx frontend/src/app/locales
git commit -m "feat(frontend): add the three missing panel skeletons

Geometry copied from the content each stands in for rather than
approximated. On a 22-second cold start these are on screen for seconds,
so a wrong-sized placeholder is a visible shove rather than a flicker.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 11: Overview stats — adapter, new stat set, wiring

**Files:**
- Create: `frontend/src/features/overview/adapt.ts`
- Test: `frontend/src/features/overview/adapt.test.ts`
- Modify: `frontend/src/features/overview/useOverviewStats.ts`
- Modify: `frontend/src/features/overview/useOverviewStats.test.ts` (rewrite)
- Modify: `frontend/src/features/overview/StatTilesRow.tsx`
- Modify: `frontend/src/app/locales/en.json`, `frontend/src/app/locales/fil.json`

**Interfaces:**
- Consumes: `useApiResource`, `ApiResource` from `@/lib/api/useApiResource`; `StatTilesSkeleton` from Task 10; `PanelState` from Task 9.
- Produces: `adaptOverviewStats(raw: unknown): OverviewStat[]`; `useOverviewStats(): ApiResource<OverviewStat[]>`.

**The stat set changes.** The wire serves `activeOrders`, `spend`, `suppliers`, `avgPrice`. The
fixture served `new_inquiries`, `pending_orders`, `saved_lots`, `spend_this_month`. Only spend
overlaps, and `saved_lots` and `new_inquiries` have no backing table — they could only ever have
been faked.

- [ ] **Step 1: Swap the stat copy in `en.json`**

Replace the four lines `"stats.new_inquiries"` … `"stats.spend_this_month"` with:

```json
  "stats.activeOrders": "Active Orders",
  "stats.spend": "Spend (30 Days)",
  "stats.suppliers": "Suppliers Traded With",
  "stats.avgPrice": "Avg. Market Price",
```

Also add, after `"stats.loading"`:

```json
  "stats.empty": "No figures to show yet.",
```

- [ ] **Step 2: Swap the same keys in `fil.json`**

Replace the corresponding four lines with:

```json
  "stats.activeOrders": "Mga Aktibong Order",
  "stats.spend": "Gastos (30 Araw)",
  "stats.suppliers": "Mga Supplier na Nakatransaksyon",
  "stats.avgPrice": "Karaniwang Presyo sa Merkado",
```

and add after `"stats.loading"`:

```json
  "stats.empty": "Wala pang mga bilang na maipapakita.",
```

- [ ] **Step 3: Write the failing adapter test**

Create `frontend/src/features/overview/adapt.test.ts`:

```ts
import { describe, expect, it, vi } from 'vitest'
import { adaptOverviewStats } from './adapt'

const wire = (stats: unknown) => ({ stats })

describe('adaptOverviewStats', () => {
  it('joins each wire key to its presentation fields', () => {
    const result = adaptOverviewStats(
      wire([{ key: 'spend', value: 2_671_400, deltaPercent: 18 }]),
    )

    expect(result).toHaveLength(1)
    expect(result[0]).toMatchObject({
      key: 'spend',
      labelKey: 'stats.spend',
      value: 2_671_400,
      deltaPercent: 18,
      format: 'currency',
    })
    expect(result[0].icon).toBeTypeOf('object')
  })

  it('preserves the order the wire sent, which is the layout order', () => {
    const result = adaptOverviewStats(
      wire([
        { key: 'activeOrders', value: 1, deltaPercent: 0 },
        { key: 'spend', value: 2, deltaPercent: 0 },
        { key: 'suppliers', value: 3, deltaPercent: 0 },
        { key: 'avgPrice', value: 4, deltaPercent: 0 },
      ]),
    )

    expect(result.map((s) => s.key)).toEqual(['activeOrders', 'spend', 'suppliers', 'avgPrice'])
  })

  it('marks spend and avgPrice as currency and the rest as counts', () => {
    const result = adaptOverviewStats(
      wire([
        { key: 'activeOrders', value: 1, deltaPercent: 0 },
        { key: 'spend', value: 2, deltaPercent: 0 },
        { key: 'suppliers', value: 3, deltaPercent: 0 },
        { key: 'avgPrice', value: 4, deltaPercent: 0 },
      ]),
    )

    expect(result.map((s) => s.format)).toEqual(['count', 'currency', 'count', 'currency'])
  })

  it('treats rising spend as bad and the rest as good', () => {
    // upIsGood drives the delta chip's colour. More spend is not an achievement, and a green
    // "+18%" beside a buyer's outgoings would congratulate them for it.
    const result = adaptOverviewStats(
      wire([
        { key: 'activeOrders', value: 1, deltaPercent: 0 },
        { key: 'spend', value: 2, deltaPercent: 0 },
        { key: 'avgPrice', value: 3, deltaPercent: 0 },
      ]),
    )

    expect(result.map((s) => s.upIsGood)).toEqual([true, false, false])
  })

  it('drops an unknown key and warns, rather than rendering a blank tile', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    const result = adaptOverviewStats(
      wire([
        { key: 'spend', value: 1, deltaPercent: 0 },
        { key: 'somethingNew', value: 2, deltaPercent: 0 },
      ]),
    )

    expect(result.map((s) => s.key)).toEqual(['spend'])
    expect(warn).toHaveBeenCalledOnce()
    warn.mockRestore()
  })

  it('returns an empty array for an empty payload', () => {
    expect(adaptOverviewStats(wire([]))).toEqual([])
  })

  it('throws on a payload that is not shaped like the contract', () => {
    // Thrown, not defaulted to []. An empty array renders the empty state, which says "you have
    // no data" — a different and wronger claim than "we could not read the response".
    expect(() => adaptOverviewStats({ nope: true })).toThrow()
    expect(() => adaptOverviewStats(null)).toThrow()
  })
})
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/features/overview/adapt.test.ts`
Expected: FAIL — cannot resolve `./adapt`.

- [ ] **Step 5: Write the adapter**

Create `frontend/src/features/overview/adapt.ts`:

```ts
import { Coins, LineChart, ShoppingCart, Users } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type { OverviewStat } from './types'

/** One wire stat: `OverviewStatDto(string Key, decimal Value, decimal DeltaPercent)`. */
interface OverviewStatWire {
  key: string
  value: number
  deltaPercent: number
}

/**
 * Everything the wire deliberately does not carry.
 *
 * Icons, labels and "is up good" are presentation decisions. Putting them on the DTO would mean
 * a backend deploy to rename a tile or swap an icon, and would make the API's contract depend on
 * a component library it has never heard of.
 */
interface StatPresentation {
  labelKey: string
  icon: LucideIcon
  format: OverviewStat['format']
  upIsGood: boolean
}

const PRESENTATION: Record<string, StatPresentation> = {
  activeOrders: {
    labelKey: 'stats.activeOrders',
    icon: ShoppingCart,
    format: 'count',
    upIsGood: true,
  },
  spend: {
    labelKey: 'stats.spend',
    icon: Coins,
    format: 'currency',
    // More spend is not an achievement. A green "+18%" beside a buyer's outgoings congratulates
    // them for paying more.
    upIsGood: false,
  },
  suppliers: {
    labelKey: 'stats.suppliers',
    icon: Users,
    format: 'count',
    upIsGood: true,
  },
  avgPrice: {
    labelKey: 'stats.avgPrice',
    icon: LineChart,
    format: 'currency',
    // A rising market price is bad news for a buyer, which is who this dashboard is for.
    upIsGood: false,
  },
}

function isStatArray(value: unknown): value is { stats: OverviewStatWire[] } {
  return (
    value !== null &&
    typeof value === 'object' &&
    Array.isArray((value as { stats?: unknown }).stats)
  )
}

/**
 * Wire stats → tile models, joined against the presentation table above.
 *
 * Pure, and that is the point: the join is the part most likely to be wrong, and this way it is
 * tested with plain objects and no network at all.
 */
export function adaptOverviewStats(raw: unknown): OverviewStat[] {
  if (!isStatArray(raw)) {
    /*
     * Throwing rather than returning []. An empty array renders the empty state, which tells the
     * reader they have no data — a specific and false claim. `useApiResource` turns this into a
     * `malformed` failure, which says the truth: we could not read the response.
     */
    throw new TypeError('Overview stats response did not carry a `stats` array.')
  }

  return raw.stats.flatMap((stat) => {
    const presentation = PRESENTATION[stat.key]

    if (!presentation) {
      // Dropped, not rendered blank. An unlabelled tile is worse than a shorter row, and the
      // warning is what tells a developer the backend grew a key the frontend has not learned.
      console.warn(`Unknown overview stat key from the API, dropping it: ${stat.key}`)
      return []
    }

    return [
      {
        key: stat.key,
        value: stat.value,
        deltaPercent: stat.deltaPercent,
        ...presentation,
      },
    ]
  })
}
```

- [ ] **Step 6: Run the adapter test to verify it passes**

Run: `cd frontend && npx vitest run src/features/overview/adapt.test.ts`
Expected: PASS, 7 tests.

- [ ] **Step 7: Rewrite the hook**

Replace the entire contents of `frontend/src/features/overview/useOverviewStats.ts`:

```ts
import { useApiResource, type ApiResource } from '@/lib/api/useApiResource'
import { adaptOverviewStats } from './adapt'
import type { OverviewStat } from './types'

/**
 * The four buyer stat tiles, from `GET /api/v1/buyer/overview/stats`.
 *
 * The return shape changed in Phase I, breaking a promise the previous doc comment made, and the
 * break was deliberate. The old `{ stats, isLoading }` had `isLoading` hardcoded to `false` and
 * read by nobody, and it could not express "failed" — which on a free-tier deployment with a
 * 22-second cold start is the state that matters most.
 */
export function useOverviewStats(): ApiResource<OverviewStat[]> {
  return useApiResource('/api/v1/buyer/overview/stats', { adapt: adaptOverviewStats })
}
```

- [ ] **Step 8: Rewrite the hook test**

Replace the entire contents of `frontend/src/features/overview/useOverviewStats.test.ts`:

```ts
import { renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { installFetchStub } from '@/lib/api/testing'
import { useOverviewStats } from './useOverviewStats'

afterEach(() => {
  installFetchStub([]).restore()
})

describe('useOverviewStats', () => {
  it('fetches the overview stats endpoint', async () => {
    const stub = installFetchStub([{ ok: true, body: { stats: [] } }])

    const { result } = renderHook(() => useOverviewStats())
    await waitFor(() => expect(result.current.status).toBe('success'))

    expect(stub.calls[0]).toContain('/api/v1/buyer/overview/stats')
  })

  it('returns adapted tiles on success', async () => {
    installFetchStub([
      {
        ok: true,
        body: {
          stats: [
            { key: 'activeOrders', value: 12, deltaPercent: 8.5 },
            { key: 'spend', value: 2_671_400, deltaPercent: 18 },
          ],
        },
      },
    ])

    const { result } = renderHook(() => useOverviewStats())
    await waitFor(() => expect(result.current.status).toBe('success'))

    if (result.current.status !== 'success') throw new Error('expected success')
    expect(result.current.data.map((s) => s.key)).toEqual(['activeOrders', 'spend'])
    expect(result.current.data[0].labelKey).toBe('stats.activeOrders')
  })

  it('reports an error when the request fails', async () => {
    installFetchStub([{ ok: true, body: {}, status: 400 }])

    const { result } = renderHook(() => useOverviewStats())
    await waitFor(() => expect(result.current.status).toBe('error'))
  })
})
```

- [ ] **Step 9: Wire the panel**

Replace the entire contents of `frontend/src/features/overview/StatTilesRow.tsx`:

```tsx
import { useTranslation } from 'react-i18next'
import { PanelState } from '@/components/PanelState'
import { StatTile } from './StatTile'
import { StatTilesSkeleton } from './StatTilesSkeleton'
import { useOverviewStats } from './useOverviewStats'

/**
 * The stat band at the top of Overview. Owns the grid; the tile owns itself.
 * Four across on desktop, two on tablet, stacked on a phone — the mockup's
 * four-up row is unreadable below ~640px.
 */
export function StatTilesRow() {
  const { t } = useTranslation()
  const resource = useOverviewStats()

  if (resource.status !== 'success') {
    return (
      <PanelState
        resource={resource}
        title={t('overview.subtitle')}
        skeleton={<StatTilesSkeleton />}
      />
    )
  }

  if (resource.data.length === 0) {
    return <p className="text-sm text-muted-fg">{t('stats.empty')}</p>
  }

  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
      {resource.data.map((stat) => (
        <StatTile key={stat.key} stat={stat} />
      ))}
    </div>
  )
}
```

- [ ] **Step 10: Run the feature's tests**

Run: `cd frontend && npx vitest run src/features/overview src/app/locales`
Expected: PASS. If `StatTile.test.tsx` or `accessible-names.test.tsx` reference the removed
`stats.new_inquiries` copy, update those references to the new keys — the labels changed, the
components did not.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/features/overview frontend/src/app/locales
git commit -m "feat(frontend): wire the stat tiles to the live API

Adopts the backend's stat set. The fixture's saved_lots and
new_inquiries had no backing table and could only ever have been faked;
activeOrders, spend, suppliers and avgPrice are computed from real rows.

An unknown key is dropped with a warning rather than rendered blank — an
unlabelled tile is worse than a shorter row.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 12: Price trends — adapter, 6/12 ranges, wiring

**Files:**
- Create: `frontend/src/features/pricing/adapt.ts`
- Test: `frontend/src/features/pricing/adapt.test.ts`
- Modify: `frontend/src/features/pricing/types.ts`
- Modify: `frontend/src/features/pricing/useMarketPriceTrends.ts`
- Modify: `frontend/src/features/pricing/useMarketPriceTrends.test.ts` (rewrite)
- Modify: `frontend/src/features/pricing/MarketPriceTrendsPanel.tsx`
- Delete: `frontend/src/features/pricing/fixtures.ts`, `frontend/src/features/pricing/fixtures.test.ts`
- Modify: `frontend/src/app/locales/en.json`, `frontend/src/app/locales/fil.json`

**Interfaces:**
- Consumes: `useApiResource`, `ApiResource`; `PanelState`; `MarketPriceTrendsSkeleton` (already exists).
- Produces: `adaptPriceTrends(raw: unknown): PricePoint[]`; `useMarketPriceTrends(months: RangeMonths): ApiResource<PricePoint[]>`; `RANGE_MONTHS = [6, 12] as const`.

**Why the 3-month range goes.** The backend pivots monthly and the seed holds exactly 12 months,
so a 3-month range renders three points — which reads as a broken chart rather than a trend.

- [ ] **Step 1: Update the locale copy in both files**

In `en.json`: delete the `"pricing.range_3"` line, add after `"pricing.loading"`:

```json
  "pricing.empty": "No price history for this range yet.",
```

and replace `"pricing.chart_label"` — it currently says "Weekly", which stops being true:

```json
  "pricing.chart_label": "Monthly wholesale price per kilo for rice, corn and vegetables over the selected range.",
```

In `fil.json`: delete `"pricing.range_3"`, add:

```json
  "pricing.empty": "Wala pang kasaysayan ng presyo para sa saklaw na ito.",
```

and replace:

```json
  "pricing.chart_label": "Buwanang presyo bawat kilo ng bigas, mais at gulay sa napiling saklaw.",
```

- [ ] **Step 2: Write the failing adapter test**

Create `frontend/src/features/pricing/adapt.test.ts`:

```ts
import { describe, expect, it } from 'vitest'
import { adaptPriceTrends } from './adapt'

describe('adaptPriceTrends', () => {
  it('flattens the nested prices dictionary into one row per date', () => {
    const result = adaptPriceTrends({
      points: [{ date: '2026-08-01', prices: { rice: 51.2, corn: 23.4, vegetables: 71 } }],
    })

    expect(result).toEqual([{ date: '2026-08-01', rice: 51.2, corn: 23.4, vegetables: 71 }])
  })

  it('defaults a crop absent from a point to zero', () => {
    // Matching the backend's own MissingPrice convention. No crop trades at PHP 0/kg, so the
    // line drops to the floor and reads on sight as missing data rather than a market event.
    const result = adaptPriceTrends({ points: [{ date: '2026-08-01', prices: { rice: 51 } }] })

    expect(result[0]).toEqual({ date: '2026-08-01', rice: 51, corn: 0, vegetables: 0 })
  })

  it('ignores crops the chart has no series for', () => {
    const result = adaptPriceTrends({
      points: [{ date: '2026-08-01', prices: { rice: 51, mangoes: 180 } }],
    })

    expect(result[0]).not.toHaveProperty('mangoes')
  })

  it('preserves order, which the chart depends on', () => {
    const result = adaptPriceTrends({
      points: [
        { date: '2026-07-01', prices: {} },
        { date: '2026-08-01', prices: {} },
      ],
    })

    expect(result.map((p) => p.date)).toEqual(['2026-07-01', '2026-08-01'])
  })

  it('returns an empty array for an empty payload', () => {
    expect(adaptPriceTrends({ points: [] })).toEqual([])
  })

  it('throws on a payload that is not shaped like the contract', () => {
    expect(() => adaptPriceTrends({ nope: true })).toThrow()
    expect(() => adaptPriceTrends(null)).toThrow()
  })
})
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/features/pricing/adapt.test.ts`
Expected: FAIL — cannot resolve `./adapt`.

- [ ] **Step 4: Write the adapter**

Create `frontend/src/features/pricing/adapt.ts`:

```ts
import type { SeriesKey } from '@/lib/chart-theme'
import type { PricePoint } from './types'

/** One wire point: `PricePointDto(string Date, IReadOnlyDictionary<string, decimal> Prices)`. */
interface PricePointWire {
  date: string
  prices: Record<string, number>
}

/** The series the chart draws. A crop outside this list has no colour and no legend entry. */
const SERIES_KEYS: SeriesKey[] = ['rice', 'corn', 'vegetables']

/** What a crop gets in a month it has no observation for. Mirrors the backend's MissingPrice. */
const MISSING_PRICE = 0

function isTrendsPayload(value: unknown): value is { points: PricePointWire[] } {
  return (
    value !== null &&
    typeof value === 'object' &&
    Array.isArray((value as { points?: unknown }).points)
  )
}

/**
 * Nested wire points → the flat row-per-x-value shape Recharts' `data` prop expects.
 *
 * The wire is nested because a dictionary is the honest representation of "a price per crop" and
 * survives a crop being added. The chart is flat because that is what Recharts consumes.
 * Reshaping here, once, beats reshaping in the component on every render.
 */
export function adaptPriceTrends(raw: unknown): PricePoint[] {
  if (!isTrendsPayload(raw)) {
    throw new TypeError('Price trends response did not carry a `points` array.')
  }

  return raw.points.map((point) => {
    const row = { date: point.date } as PricePoint

    // Iterating the SERIES the chart knows about, not the keys the payload happens to carry.
    // Driving it from the payload would let a missing crop silently vanish from the legend for
    // the whole range, which looks exactly like a working chart.
    for (const key of SERIES_KEYS) {
      row[key] = point.prices?.[key] ?? MISSING_PRICE
    }

    return row
  })
}
```

- [ ] **Step 5: Run the adapter test to verify it passes**

Run: `cd frontend && npx vitest run src/features/pricing/adapt.test.ts`
Expected: PASS, 6 tests.

- [ ] **Step 6: Update the types**

In `frontend/src/features/pricing/types.ts`, replace lines 10-16 with:

```ts
/**
 * The ranges the chart offers.
 *
 * 3 months was dropped in Phase I. The API pivots monthly, so a 3-month range is three points —
 * a chart that reads as broken rather than as a trend. The seeded history is 12 months, which is
 * also the honest ceiling here.
 */
export const RANGE_MONTHS = [6, 12] as const
export type RangeMonths = (typeof RANGE_MONTHS)[number]
```

`MarketPriceTrendsResult` is deleted — the hook now returns `ApiResource<PricePoint[]>`. Also
update the `PricePoint` doc comment on line 3-7, replacing "One week's closing price" with "One
month's average price".

- [ ] **Step 7: Rewrite the hook**

Replace the entire contents of `frontend/src/features/pricing/useMarketPriceTrends.ts`:

```ts
import { useApiResource, type ApiResource } from '@/lib/api/useApiResource'
import { adaptPriceTrends } from './adapt'
import type { PricePoint, RangeMonths } from './types'

/**
 * Market price history, from `GET /api/v1/pricing/trends?months=`.
 *
 * `months` goes to the server rather than slicing a longer client-side series: the window is a
 * data question, and asking for 6 months and then trimming 12 wastes the difference on a rural
 * connection.
 */
export function useMarketPriceTrends(months: RangeMonths): ApiResource<PricePoint[]> {
  return useApiResource('/api/v1/pricing/trends', {
    query: { months },
    adapt: adaptPriceTrends,
  })
}
```

- [ ] **Step 8: Rewrite the hook test**

Replace the entire contents of `frontend/src/features/pricing/useMarketPriceTrends.test.ts`:

```ts
import { renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { installFetchStub } from '@/lib/api/testing'
import { useMarketPriceTrends } from './useMarketPriceTrends'

afterEach(() => {
  vi.unstubAllGlobals()
})

const payload = {
  ok: true as const,
  body: { points: [{ date: '2026-08-01', prices: { rice: 51, corn: 23, vegetables: 71 } }] },
}

describe('useMarketPriceTrends', () => {
  it('sends the requested range to the server', async () => {
    const stub = installFetchStub([payload])

    const { result } = renderHook(() => useMarketPriceTrends(12))
    await waitFor(() => expect(result.current.status).toBe('success'))

    expect(stub.calls[0]).toContain('months=12')
  })

  it('returns flattened points', async () => {
    installFetchStub([payload])

    const { result } = renderHook(() => useMarketPriceTrends(6))
    await waitFor(() => expect(result.current.status).toBe('success'))

    if (result.current.status !== 'success') throw new Error('expected success')
    expect(result.current.data[0]).toEqual({
      date: '2026-08-01',
      rice: 51,
      corn: 23,
      vegetables: 71,
    })
  })

  it('refetches when the range changes', async () => {
    const stub = installFetchStub([payload])

    const { rerender, result } = renderHook(({ months }) => useMarketPriceTrends(months), {
      initialProps: { months: 6 as 6 | 12 },
    })
    await waitFor(() => expect(result.current.status).toBe('success'))

    rerender({ months: 12 })
    await waitFor(() => expect(stub.calls).toHaveLength(2))

    expect(stub.calls[1]).toContain('months=12')
  })
})
```

- [ ] **Step 9: Wire the panel**

In `frontend/src/features/pricing/MarketPriceTrendsPanel.tsx`:

Add these imports at the top:

```tsx
import { PanelState } from '@/components/PanelState'
import { MarketPriceTrendsSkeleton } from './MarketPriceTrendsSkeleton'
```

Replace line 15 (`const { points } = useMarketPriceTrends(months)`) with:

```tsx
  const resource = useMarketPriceTrends(months)
```

Then, immediately before the `return (` on line 54, insert:

```tsx
  if (resource.status !== 'success') {
    return (
      <PanelState
        resource={resource}
        title={t('pricing.title')}
        skeleton={<MarketPriceTrendsSkeleton />}
      />
    )
  }

  const points = resource.data
```

Finally, add an empty state. Replace the `<ChartContainer ...>` block's opening so the chart is
guarded — insert immediately after the `<p className="mt-4 text-xs text-muted-fg">` axis-label
line:

```tsx
      {points.length === 0 ? (
        <p className="mt-4 text-sm text-muted-fg">{t('pricing.empty')}</p>
      ) : (
```

and close it immediately after the `</ChartContainer>` closing tag:

```tsx
      )}
```

Note: `config`, `tickLabel` and `tooltipLabel` are declared between the hook call and the return.
They must stay **above** the `if (resource.status !== 'success')` early return, or they will be
declared after a conditional return and fail to compile. Move the early return to sit directly
after `const resource = ...` only if `config`/`tickLabel`/`tooltipLabel` are moved with it;
otherwise leave the early return where the `return (` was, as described above.

- [ ] **Step 10: Delete the fixtures**

```bash
rm frontend/src/features/pricing/fixtures.ts frontend/src/features/pricing/fixtures.test.ts
```

`fixtures.test.ts` pinned `WEEKLY_PRICES` by value and `WEEKS_PER_RANGE` as `{3:13, 6:26, 12:52}`.
Both are gone: the series is now monthly and comes from the server, so there is no generated walk
left to pin.

- [ ] **Step 11: Run the feature's tests**

Run: `cd frontend && npx vitest run src/features/pricing src/app/locales`
Expected: PASS. `MarketPriceTrendsPanel.test.tsx` will need its fixture-based setup replaced with
`installFetchStub` and `waitFor`; keep every existing assertion about the legend, the axis and the
range selector, and change only how the data arrives. The range-selector test must expect **two**
options now, not three.

- [ ] **Step 12: Commit**

```bash
git add frontend/src/features/pricing frontend/src/app/locales
git commit -m "feat(frontend): wire the price chart to the live API

Ranges drop to 6 and 12 months. The API pivots monthly and the seed
holds 12 months, so a 3-month range was three points — a chart that
reads as broken rather than as a trend.

The adapter iterates the series the chart knows about, not the keys the
payload carries: driving it from the payload would let a missing crop
vanish from the legend for the whole range and still look like a
working chart.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 13: Nearby suppliers — adapter, hook, wiring

**Files:**
- Create: `frontend/src/features/suppliers/adapt.ts`
- Test: `frontend/src/features/suppliers/adapt.test.ts`
- Modify: `frontend/src/features/suppliers/types.ts`, `useNearbySuppliers.ts`, `useNearbySuppliers.test.ts`, `NearbySuppliersPanel.tsx`
- Delete: `frontend/src/features/suppliers/fixtures.ts`

**Interfaces:**
- Consumes: `useApiResource`, `ApiResource`; `PanelState`; `SupplierMapSkeleton` (already exists); `LatLng` from `@/lib/geo`.
- Produces: `adaptNearbySuppliers(raw: unknown): NearbySuppliersPayload` where
  `interface NearbySuppliersPayload { origin: LatLng; suppliers: NearbySupplier[] }`;
  `useNearbySuppliers(): ApiResource<NearbySuppliersPayload>`.

**The endpoint requires `lat` and `lng`** — they have no defaults and a missing one is a 400. The
buyer's location is the fixture's `BUYER_LOCATION` (`{ lat: 14.676, lng: 121.0437 }`), which moves
into the hook as a constant. There is no geolocation in this phase.

- [ ] **Step 1: Write the failing adapter test**

Create `frontend/src/features/suppliers/adapt.test.ts`:

```ts
import { describe, expect, it } from 'vitest'
import { adaptNearbySuppliers } from './adapt'

const wire = {
  origin: { lat: 14.676, lng: 121.0437 },
  suppliers: [
    {
      id: '3',
      name: 'Bataan Rice Growers',
      region: 'Balanga, Bataan',
      location: { lat: 14.68, lng: 120.54 },
      verified: true,
      crops: ['rice'],
      distanceKm: 53.2,
    },
  ],
}

describe('adaptNearbySuppliers', () => {
  it('maps the payload one-to-one', () => {
    const result = adaptNearbySuppliers(wire)

    expect(result.origin).toEqual({ lat: 14.676, lng: 121.0437 })
    expect(result.suppliers).toHaveLength(1)
    expect(result.suppliers[0]).toMatchObject({
      id: '3',
      name: 'Bataan Rice Growers',
      distanceKm: 53.2,
      verified: true,
    })
  })

  it('preserves the server ordering, which is nearest-first', () => {
    // The distance is computed server-side now. Re-sorting here would be a second opinion that
    // the response can silently contradict, and the map and list share this one array precisely
    // so a pin and its row cannot disagree.
    const result = adaptNearbySuppliers({
      origin: wire.origin,
      suppliers: [
        { ...wire.suppliers[0], id: 'a', distanceKm: 10 },
        { ...wire.suppliers[0], id: 'b', distanceKm: 5 },
      ],
    })

    expect(result.suppliers.map((s) => s.id)).toEqual(['a', 'b'])
  })

  it('keeps a supplier with no crops rather than dropping it', () => {
    const result = adaptNearbySuppliers({
      origin: wire.origin,
      suppliers: [{ ...wire.suppliers[0], crops: [] }],
    })

    expect(result.suppliers[0].crops).toEqual([])
  })

  it('returns an empty list for an empty payload', () => {
    expect(adaptNearbySuppliers({ origin: wire.origin, suppliers: [] }).suppliers).toEqual([])
  })

  it('throws on a payload missing its origin', () => {
    // The map cannot centre without it, and defaulting to a coordinate would put the "your
    // location" pin somewhere the buyer is not.
    expect(() => adaptNearbySuppliers({ suppliers: [] })).toThrow()
    expect(() => adaptNearbySuppliers(null)).toThrow()
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/features/suppliers/adapt.test.ts`
Expected: FAIL — cannot resolve `./adapt`.

- [ ] **Step 3: Write the adapter**

Create `frontend/src/features/suppliers/adapt.ts`:

```ts
import type { LatLng } from '@/lib/geo'
import type { SeriesKey } from '@/lib/chart-theme'
import type { NearbySupplier } from './types'

export interface NearbySuppliersPayload {
  origin: LatLng
  suppliers: NearbySupplier[]
}

interface NearbySupplierWire {
  id: string
  name: string
  region: string
  location: { lat: number; lng: number }
  verified: boolean
  crops: string[]
  distanceKm: number
}

function isPayload(value: unknown): value is { origin: LatLng; suppliers: NearbySupplierWire[] } {
  if (value === null || typeof value !== 'object') {
    return false
  }

  const candidate = value as { origin?: unknown; suppliers?: unknown }

  return (
    Array.isArray(candidate.suppliers) &&
    candidate.origin !== null &&
    typeof candidate.origin === 'object' &&
    typeof (candidate.origin as LatLng).lat === 'number' &&
    typeof (candidate.origin as LatLng).lng === 'number'
  )
}

/**
 * Wire suppliers → the model the map and the list share.
 *
 * `distanceKm` arrives from the server now; it used to be computed here with `haversineKm`. The
 * client no longer has an opinion about it, which removes the possibility of a pin and a row
 * disagreeing about how far away something is.
 */
export function adaptNearbySuppliers(raw: unknown): NearbySuppliersPayload {
  if (!isPayload(raw)) {
    throw new TypeError('Nearby suppliers response did not carry an origin and a suppliers array.')
  }

  return {
    origin: { lat: raw.origin.lat, lng: raw.origin.lng },
    // Order preserved: the server sorts nearest-first and re-sorting would be a second opinion.
    suppliers: raw.suppliers.map((supplier) => ({
      id: supplier.id,
      name: supplier.name,
      region: supplier.region,
      location: { lat: supplier.location.lat, lng: supplier.location.lng },
      verified: supplier.verified,
      crops: supplier.crops as SeriesKey[],
      distanceKm: supplier.distanceKm,
    })),
  }
}
```

- [ ] **Step 4: Run the adapter test to verify it passes**

Run: `cd frontend && npx vitest run src/features/suppliers/adapt.test.ts`
Expected: PASS, 5 tests.

- [ ] **Step 5: Rewrite the hook**

Replace the entire contents of `frontend/src/features/suppliers/useNearbySuppliers.ts`:

```ts
import { useApiResource, type ApiResource } from '@/lib/api/useApiResource'
import { adaptNearbySuppliers, type NearbySuppliersPayload } from './adapt'

/**
 * Where the buyer is.
 *
 * A constant, not `navigator.geolocation`. The API requires `lat` and `lng` — they have no
 * defaults and a missing one is a 400 — and a permission prompt on first paint would block the
 * whole panel behind a decision the reader has no context to make yet. Real geolocation is a
 * feature, not a wiring detail.
 */
const BUYER_LOCATION = { lat: 14.676, lng: 121.0437 } as const

/**
 * Verified suppliers near the buyer, from `GET /api/v1/suppliers/nearby?lat=&lng=`.
 *
 * One call, one array, handed to both the map and the list by the panel. That is unchanged from
 * the fixture era and is the whole design: there is no second path to this data, so a pin and
 * its row cannot disagree.
 */
export function useNearbySuppliers(): ApiResource<NearbySuppliersPayload> {
  return useApiResource('/api/v1/suppliers/nearby', {
    query: { lat: BUYER_LOCATION.lat, lng: BUYER_LOCATION.lng },
    adapt: adaptNearbySuppliers,
  })
}
```

- [ ] **Step 6: Rewrite the hook test**

Replace the entire contents of `frontend/src/features/suppliers/useNearbySuppliers.test.ts`:

```ts
import { renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { installFetchStub } from '@/lib/api/testing'
import { useNearbySuppliers } from './useNearbySuppliers'

afterEach(() => {
  vi.unstubAllGlobals()
})

const body = {
  origin: { lat: 14.676, lng: 121.0437 },
  suppliers: [
    {
      id: '3',
      name: 'Bataan Rice Growers',
      region: 'Balanga, Bataan',
      location: { lat: 14.68, lng: 120.54 },
      verified: true,
      crops: ['rice'],
      distanceKm: 53.2,
    },
  ],
}

describe('useNearbySuppliers', () => {
  it('sends both coordinates, which the endpoint requires', async () => {
    // lat and lng have no server-side defaults; omitting either is a 400, and the UI builds this
    // URL, so that 400 would be our bug.
    const stub = installFetchStub([{ ok: true, body }])

    const { result } = renderHook(() => useNearbySuppliers())
    await waitFor(() => expect(result.current.status).toBe('success'))

    expect(stub.calls[0]).toContain('lat=14.676')
    expect(stub.calls[0]).toContain('lng=121.0437')
  })

  it('returns the origin and the suppliers together', async () => {
    installFetchStub([{ ok: true, body }])

    const { result } = renderHook(() => useNearbySuppliers())
    await waitFor(() => expect(result.current.status).toBe('success'))

    if (result.current.status !== 'success') throw new Error('expected success')
    expect(result.current.data.origin).toEqual({ lat: 14.676, lng: 121.0437 })
    expect(result.current.data.suppliers[0].distanceKm).toBe(53.2)
  })
})
```

- [ ] **Step 7: Update the types**

In `frontend/src/features/suppliers/types.ts`, delete the `NearbySuppliersResult` interface — the
hook returns `ApiResource<NearbySuppliersPayload>` now. Keep `Supplier` and `NearbySupplier`.

- [ ] **Step 8: Wire the panel**

Replace the body of `frontend/src/features/suppliers/NearbySuppliersPanel.tsx`'s component,
keeping the existing doc comment above it:

```tsx
export function NearbySuppliersPanel() {
  const { t } = useTranslation()
  const resource = useNearbySuppliers()

  if (resource.status !== 'success') {
    return (
      <PanelState
        resource={resource}
        title={t('suppliers.title')}
        skeleton={<SupplierMapSkeleton />}
      />
    )
  }

  const { suppliers, origin } = resource.data

  return (
    <div className="rounded-xl bg-surface p-5">
      <h2 className="text-lg font-bold text-primary">{t('suppliers.title')}</h2>

      <div className="mt-4">
        <LazySupplierMap suppliers={suppliers} origin={origin} />
      </div>

      <div className="mt-2">
        <SupplierList suppliers={suppliers} />
      </div>
    </div>
  )
}
```

Add these imports:

```tsx
import { PanelState } from '@/components/PanelState'
import { SupplierMapSkeleton } from './SupplierMapSkeleton'
```

`SupplierList` already renders `t('suppliers.empty')` for an empty array, so no empty state is
needed here.

- [ ] **Step 9: Delete the fixtures**

```bash
rm frontend/src/features/suppliers/fixtures.ts
```

- [ ] **Step 10: Run the feature's tests**

Run: `cd frontend && npx vitest run src/features/suppliers`
Expected: PASS. `NearbySuppliersPanel.test.tsx`, `SupplierMap.test.tsx` and `SupplierList.test.tsx`
that imported `ALL_SUPPLIERS` must build their supplier objects inline or via `installFetchStub`;
keep every existing assertion.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/features/suppliers
git commit -m "feat(frontend): wire nearby suppliers to the live API

distanceKm now arrives from the server rather than being computed with
haversineKm on the client, so there is no longer a client opinion that
could disagree with a pin.

lat and lng are sent explicitly — they have no server-side defaults and
the UI builds this URL, so omitting one would be our own 400.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 14: Featured lots — adapter, hook, wiring

**Files:**
- Create: `frontend/src/features/lots/adapt.ts`
- Test: `frontend/src/features/lots/adapt.test.ts`
- Modify: `frontend/src/features/lots/types.ts`, `useFeaturedLots.ts`, `useFeaturedLots.test.ts`, `FeaturedLotsPanel.tsx`
- Delete: `frontend/src/features/lots/fixtures.ts`

**Interfaces:**
- Consumes: `useApiResource`, `ApiResource`; `PanelState`; `FeaturedLotsSkeleton` from Task 10.
- Produces: `adaptFeaturedLots(raw: unknown): Lot[]`; `useFeaturedLots(): ApiResource<Lot[]>`.

**The route is `/api/v1/listings/featured`**, not `/lots/featured`. The existing hook doc comment
names the wrong one; that is corrected here.

- [ ] **Step 1: Write the failing adapter test**

Create `frontend/src/features/lots/adapt.test.ts`:

```ts
import { describe, expect, it } from 'vitest'
import { adaptFeaturedLots } from './adapt'

const lot = {
  id: '7',
  name: 'Premium Jasmine Rice',
  crop: 'rice',
  grade: 'A',
  supplier: 'Bataan Rice Growers',
  region: 'Balanga, Bataan',
  verified: true,
  volumeKg: 24_000,
  minOrderKg: 500,
  pricePerKg: 58.5,
}

describe('adaptFeaturedLots', () => {
  it('maps the payload one-to-one', () => {
    expect(adaptFeaturedLots({ lots: [lot] })).toEqual([lot])
  })

  it('preserves order, because featuring is a merchandising decision made upstream', () => {
    const result = adaptFeaturedLots({
      lots: [
        { ...lot, id: 'a' },
        { ...lot, id: 'b' },
      ],
    })

    expect(result.map((l) => l.id)).toEqual(['a', 'b'])
  })

  it('returns an empty array for an empty payload', () => {
    expect(adaptFeaturedLots({ lots: [] })).toEqual([])
  })

  it('throws on a payload that is not shaped like the contract', () => {
    expect(() => adaptFeaturedLots({ nope: true })).toThrow()
    expect(() => adaptFeaturedLots(null)).toThrow()
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/features/lots/adapt.test.ts`
Expected: FAIL — cannot resolve `./adapt`.

- [ ] **Step 3: Write the adapter**

Create `frontend/src/features/lots/adapt.ts`:

```ts
import type { SeriesKey } from '@/lib/chart-theme'
import type { Lot } from './types'

/** `FeaturedLotDto` — one-to-one with `Lot` apart from `crop` being a plain string on the wire. */
interface FeaturedLotWire {
  id: string
  name: string
  crop: string
  grade: string
  supplier: string
  region: string
  verified: boolean
  volumeKg: number
  minOrderKg: number
  pricePerKg: number
}

function isPayload(value: unknown): value is { lots: FeaturedLotWire[] } {
  return (
    value !== null && typeof value === 'object' && Array.isArray((value as { lots?: unknown }).lots)
  )
}

/**
 * Wire lots → lot cards.
 *
 * No sort and no filter, matching what the fixture-era hook documented: "featured" is a
 * merchandising decision made upstream, and any ordering invented here would be a second opinion
 * the response then silently contradicts.
 */
export function adaptFeaturedLots(raw: unknown): Lot[] {
  if (!isPayload(raw)) {
    throw new TypeError('Featured lots response did not carry a `lots` array.')
  }

  return raw.lots.map((lot) => ({
    id: lot.id,
    name: lot.name,
    crop: lot.crop as SeriesKey,
    grade: lot.grade,
    supplier: lot.supplier,
    region: lot.region,
    verified: lot.verified,
    volumeKg: lot.volumeKg,
    minOrderKg: lot.minOrderKg,
    pricePerKg: lot.pricePerKg,
  }))
}
```

- [ ] **Step 4: Run the adapter test to verify it passes**

Run: `cd frontend && npx vitest run src/features/lots/adapt.test.ts`
Expected: PASS, 4 tests.

- [ ] **Step 5: Rewrite the hook**

Replace the entire contents of `frontend/src/features/lots/useFeaturedLots.ts`:

```ts
import { useApiResource, type ApiResource } from '@/lib/api/useApiResource'
import { adaptFeaturedLots } from './adapt'
import type { Lot } from './types'

/**
 * Featured wholesale lots, from `GET /api/v1/listings/featured`.
 *
 * Note the path: `listings`, not `lots`. The previous doc comment here named
 * `/api/v1/lots/featured`, which has never existed — the frontend calls these lots and the
 * backend calls them listings, and the wire spelling is the backend's.
 */
export function useFeaturedLots(): ApiResource<Lot[]> {
  return useApiResource('/api/v1/listings/featured', { adapt: adaptFeaturedLots })
}
```

- [ ] **Step 6: Rewrite the hook test**

Replace the entire contents of `frontend/src/features/lots/useFeaturedLots.test.ts`:

```ts
import { renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { installFetchStub } from '@/lib/api/testing'
import { useFeaturedLots } from './useFeaturedLots'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useFeaturedLots', () => {
  it('calls the listings route, not a lots route', async () => {
    const stub = installFetchStub([{ ok: true, body: { lots: [] } }])

    const { result } = renderHook(() => useFeaturedLots())
    await waitFor(() => expect(result.current.status).toBe('success'))

    expect(stub.calls[0]).toContain('/api/v1/listings/featured')
  })

  it('returns the adapted lots', async () => {
    installFetchStub([
      {
        ok: true,
        body: {
          lots: [
            {
              id: '7',
              name: 'Premium Jasmine Rice',
              crop: 'rice',
              grade: 'A',
              supplier: 'Bataan Rice Growers',
              region: 'Balanga, Bataan',
              verified: true,
              volumeKg: 24_000,
              minOrderKg: 500,
              pricePerKg: 58.5,
            },
          ],
        },
      },
    ])

    const { result } = renderHook(() => useFeaturedLots())
    await waitFor(() => expect(result.current.status).toBe('success'))

    if (result.current.status !== 'success') throw new Error('expected success')
    expect(result.current.data[0].name).toBe('Premium Jasmine Rice')
  })
})
```

- [ ] **Step 7: Update the types**

In `frontend/src/features/lots/types.ts`, delete the `FeaturedLotsResult` interface. Keep `Lot`.

- [ ] **Step 8: Wire the panel**

Replace the component body in `frontend/src/features/lots/FeaturedLotsPanel.tsx`, keeping the doc
comment:

```tsx
export function FeaturedLotsPanel() {
  const { t } = useTranslation()
  const resource = useFeaturedLots()

  if (resource.status !== 'success') {
    return (
      <PanelState
        resource={resource}
        title={t('lots.title')}
        skeleton={<FeaturedLotsSkeleton />}
      />
    )
  }

  return (
    <div className="rounded-xl bg-surface p-5">
      <h2 className="text-lg font-bold text-primary">{t('lots.title')}</h2>

      <div className="mt-4">
        <LotsScroller lots={resource.data} />
      </div>
    </div>
  )
}
```

Add these imports:

```tsx
import { PanelState } from '@/components/PanelState'
import { FeaturedLotsSkeleton } from './FeaturedLotsSkeleton'
```

`LotsScroller` already renders `t('lots.empty')` for an empty array.

- [ ] **Step 9: Delete the fixtures**

```bash
rm frontend/src/features/lots/fixtures.ts
```

- [ ] **Step 10: Run the feature's tests**

Run: `cd frontend && npx vitest run src/features/lots`
Expected: PASS. `LotCard.test.tsx`, `LotsScroller.test.tsx` and `RequestQuoteDialog.test.tsx` that
imported `FEATURED_LOTS` must construct a `Lot` inline; keep every existing assertion.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/features/lots
git commit -m "feat(frontend): wire featured lots to the live API

Corrects the route while wiring it: the hook's doc comment named
/api/v1/lots/featured, which has never existed. The frontend calls them
lots and the backend calls them listings; the wire spelling wins.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 15: Recent orders — adapter, hook, wiring

**Files:**
- Create: `frontend/src/features/orders/adapt.ts`
- Test: `frontend/src/features/orders/adapt.test.ts`
- Modify: `frontend/src/features/orders/types.ts`, `useRecentOrders.ts`, `useRecentOrders.test.ts`, `RecentOrdersPanel.tsx`
- Delete: `frontend/src/features/orders/fixtures.ts`

**Interfaces:**
- Consumes: `useApiResource`, `ApiResource`; `PanelState`; `RecentOrdersSkeleton` from Task 10.
- Produces: `adaptRecentOrders(raw: unknown): Order[]`; `useRecentOrders(limit: number): ApiResource<Order[]>`.

- [ ] **Step 1: Write the failing adapter test**

Create `frontend/src/features/orders/adapt.test.ts`:

```ts
import { describe, expect, it, vi } from 'vitest'
import { adaptRecentOrders } from './adapt'

const order = {
  id: 'ORD-2418',
  product: 'Premium Jasmine Rice',
  supplier: 'Bataan Rice Growers',
  quantityKg: 1_500,
  status: 'confirmed',
  estimatedDelivery: '2026-08-15',
}

describe('adaptRecentOrders', () => {
  it('maps the payload one-to-one', () => {
    expect(adaptRecentOrders({ orders: [order] })).toEqual([order])
  })

  it('accepts every status the badge knows about', () => {
    const statuses = ['confirmed', 'processing', 'shipped', 'delivered']
    const result = adaptRecentOrders({
      orders: statuses.map((status, i) => ({ ...order, id: `ORD-${i}`, status })),
    })

    expect(result.map((o) => o.status)).toEqual(statuses)
  })

  it('drops an order whose status the badge cannot render, and warns', () => {
    // OrderStatusBadge maps status to copy and colour. An unknown value renders an unstyled,
    // unlabelled badge in a table row that otherwise looks fine — a silent wrong answer rather
    // than a visible gap.
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    const result = adaptRecentOrders({
      orders: [order, { ...order, id: 'ORD-X', status: 'refunded' }],
    })

    expect(result.map((o) => o.id)).toEqual(['ORD-2418'])
    expect(warn).toHaveBeenCalledOnce()
    warn.mockRestore()
  })

  it('returns an empty array for an empty payload', () => {
    expect(adaptRecentOrders({ orders: [] })).toEqual([])
  })

  it('throws on a payload that is not shaped like the contract', () => {
    expect(() => adaptRecentOrders({ nope: true })).toThrow()
    expect(() => adaptRecentOrders(null)).toThrow()
  })
})
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd frontend && npx vitest run src/features/orders/adapt.test.ts`
Expected: FAIL — cannot resolve `./adapt`.

- [ ] **Step 3: Write the adapter**

Create `frontend/src/features/orders/adapt.ts`:

```ts
import type { Order, StatusKey } from './types'

/** `RecentOrderDto`. `status` is a plain string on the wire; the badge needs a known one. */
interface RecentOrderWire {
  id: string
  product: string
  supplier: string
  quantityKg: number
  status: string
  estimatedDelivery: string
}

const STATUSES: StatusKey[] = ['confirmed', 'processing', 'shipped', 'delivered']

function isPayload(value: unknown): value is { orders: RecentOrderWire[] } {
  return (
    value !== null &&
    typeof value === 'object' &&
    Array.isArray((value as { orders?: unknown }).orders)
  )
}

/**
 * Wire orders → table rows.
 *
 * No sort. The fixture-era hook documented why and it still holds: ordering by estimated delivery
 * would quietly turn "recent orders" into "next arrivals" — a different panel under the same
 * heading, which is worse than no sort at all. The server already returns newest-first.
 */
export function adaptRecentOrders(raw: unknown): Order[] {
  if (!isPayload(raw)) {
    throw new TypeError('Recent orders response did not carry an `orders` array.')
  }

  return raw.orders.flatMap((order) => {
    if (!STATUSES.includes(order.status as StatusKey)) {
      // Dropped rather than rendered. OrderStatusBadge maps status to copy and colour, so an
      // unknown value produces an unlabelled badge in a row that otherwise looks correct.
      console.warn(`Unknown order status from the API, dropping the order: ${order.status}`)
      return []
    }

    return [
      {
        id: order.id,
        product: order.product,
        supplier: order.supplier,
        quantityKg: order.quantityKg,
        status: order.status as StatusKey,
        estimatedDelivery: order.estimatedDelivery,
      },
    ]
  })
}
```

- [ ] **Step 4: Run the adapter test to verify it passes**

Run: `cd frontend && npx vitest run src/features/orders/adapt.test.ts`
Expected: PASS, 5 tests.

- [ ] **Step 5: Rewrite the hook**

Replace the entire contents of `frontend/src/features/orders/useRecentOrders.ts`:

```ts
import { useApiResource, type ApiResource } from '@/lib/api/useApiResource'
import { adaptRecentOrders } from './adapt'
import type { Order } from './types'

/**
 * The most recent orders, from `GET /api/v1/orders/recent?limit=`.
 *
 * `limit` goes to the server, and the panel must not re-slice on top of it. Asking for more than
 * exists returns everything and is not an error: an account with three orders is a new account,
 * not a broken one.
 */
export function useRecentOrders(limit: number): ApiResource<Order[]> {
  return useApiResource('/api/v1/orders/recent', {
    query: { limit },
    adapt: adaptRecentOrders,
  })
}
```

- [ ] **Step 6: Rewrite the hook test**

Replace the entire contents of `frontend/src/features/orders/useRecentOrders.test.ts`:

```ts
import { renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { installFetchStub } from '@/lib/api/testing'
import { useRecentOrders } from './useRecentOrders'

afterEach(() => {
  vi.unstubAllGlobals()
})

const body = {
  orders: [
    {
      id: 'ORD-2418',
      product: 'Premium Jasmine Rice',
      supplier: 'Bataan Rice Growers',
      quantityKg: 1_500,
      status: 'confirmed',
      estimatedDelivery: '2026-08-15',
    },
  ],
}

describe('useRecentOrders', () => {
  it('sends the limit to the server rather than slicing locally', async () => {
    const stub = installFetchStub([{ ok: true, body }])

    const { result } = renderHook(() => useRecentOrders(5))
    await waitFor(() => expect(result.current.status).toBe('success'))

    expect(stub.calls[0]).toContain('limit=5')
  })

  it('returns the adapted orders', async () => {
    installFetchStub([{ ok: true, body }])

    const { result } = renderHook(() => useRecentOrders(5))
    await waitFor(() => expect(result.current.status).toBe('success'))

    if (result.current.status !== 'success') throw new Error('expected success')
    expect(result.current.data[0].id).toBe('ORD-2418')
  })

  it('refetches when the limit changes', async () => {
    const stub = installFetchStub([{ ok: true, body }])

    const { rerender, result } = renderHook(({ limit }) => useRecentOrders(limit), {
      initialProps: { limit: 5 },
    })
    await waitFor(() => expect(result.current.status).toBe('success'))

    rerender({ limit: 10 })
    await waitFor(() => expect(stub.calls).toHaveLength(2))

    expect(stub.calls[1]).toContain('limit=10')
  })
})
```

- [ ] **Step 7: Update the types**

In `frontend/src/features/orders/types.ts`, delete the `RecentOrdersResult` interface. Keep `Order`
and `StatusKey`.

- [ ] **Step 8: Wire the panel**

In `frontend/src/features/orders/RecentOrdersPanel.tsx`:

Add these imports:

```tsx
import { PanelState } from '@/components/PanelState'
import { RecentOrdersSkeleton } from './RecentOrdersSkeleton'
```

Replace line 38 (`const { orders } = useRecentOrders(limit)`) with:

```tsx
  const resource = useRecentOrders(limit)
```

Then insert immediately before the `return (` on line 41:

```tsx
  if (resource.status !== 'success') {
    return (
      <PanelState
        resource={resource}
        title={t('orders.title')}
        skeleton={<RecentOrdersSkeleton />}
      />
    )
  }

  const orders = resource.data
```

`useId` must stay above the early return — it is a hook. Everything else in the JSX, including the
existing `orders.length === 0` empty state, is unchanged.

- [ ] **Step 9: Delete the fixtures**

```bash
rm frontend/src/features/orders/fixtures.ts
```

- [ ] **Step 10: Run the feature's tests**

Run: `cd frontend && npx vitest run src/features/orders`
Expected: PASS. `RecentOrdersPanel.test.tsx` needs `installFetchStub` plus `waitFor`; keep every
existing assertion about the table markup, `<th scope="row">`, and the "View all" link.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/features/orders
git commit -m "feat(frontend): wire recent orders to the live API

An order with a status the badge cannot render is dropped with a
warning: OrderStatusBadge maps status to copy and colour, so an unknown
value produces an unlabelled badge in a row that otherwise looks fine.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 16: Full-suite green

**Files:**
- Modify: whichever test files still reference deleted fixtures.
- Delete: `frontend/src/features/overview/fixtures.ts` (the last one).

**Interfaces:**
- Consumes: everything from Tasks 6-15.
- Produces: a green suite, a clean build, and no orphaned fixture imports.

- [ ] **Step 1: Delete the last fixture and find every remaining reference**

```bash
rm frontend/src/features/overview/fixtures.ts
cd frontend && grep -rn "fixtures\|BUYER_STATS\|WEEKLY_PRICES\|WEEKS_PER_RANGE\|ALL_SUPPLIERS\|BUYER_LOCATION\|FEATURED_LOTS\|RECENT_ORDERS" src/ || echo "no references remain"
```

Expected: `no references remain`. Any hit is a test still importing a deleted module — replace the
import with an inline literal of the same shape.

- [ ] **Step 2: Run the whole frontend suite**

Run: `cd frontend && npm test`
Expected: PASS, all files.

Failures at this point are almost all one of three kinds:
- A component test asserting the old stat labels — update to the new keys.
- A panel test rendering synchronously — wrap in `installFetchStub` + `waitFor`.
- A range-selector test expecting three options — it is two now.

**Do not** silence a failure by loosening an assertion. Every one of these tests was written to
pin something; if the thing it pinned still matters, the test should still pin it.

- [ ] **Step 3: Verify the three cross-cutting guards specifically**

Run: `cd frontend && npx vitest run src/lib/no-raw-hex.test.ts src/app/locales/locales.test.ts src/lib/palette.test.ts`
Expected: PASS, all three.

- [ ] **Step 4: Typecheck, lint, build**

Run: `cd frontend && npm run typecheck && npm run lint && npm run build`
Expected: all clean.

- [ ] **Step 5: Confirm no dependency crept in**

```bash
cd frontend && git diff --stat HEAD~10 -- package.json package-lock.json
```

Expected: no changes to `dependencies` or `devDependencies`. The hand-rolled layer exists so this
stays true.

- [ ] **Step 6: Run the backend suite once more**

Run: `dotnet test backend/AniKo_API.Tests`
Expected: PASS, 411 tests, 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add -A frontend/src
git commit -m "test(frontend): retire the fixtures and settle the suite on the API

Every panel test now drives its data through the fetch stub. No
assertion was loosened to make this pass — the tests that pinned
markup, accessibility and copy still pin them.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 17: Deploy and verify against the success criteria

**Files:** none.

**Interfaces:**
- Consumes: everything.
- Produces: a live, wired dashboard.

- [ ] **Step 1: Set the environment variable on Netlify**

Set `VITE_API_BASE_URL=https://aniko-api-4emi.onrender.com` in the Netlify site environment for
the `aniko-agri` site, scoped to all deploy contexts. Use the Netlify MCP project-services-updater
tool, or the site's Environment variables settings page.

**This must be set before the deploy**, not after. Vite inlines `import.meta.env` at build time,
and `config.ts` throws in a production build when it is missing — a deploy without it fails at
build or serves a bundle that throws on load.

- [ ] **Step 2: Push**

```bash
git push origin main
git rev-parse --short HEAD
```

- [ ] **Step 3: Confirm the backend is serving this commit**

```bash
curl -s https://aniko-api-4emi.onrender.com/ | python -m json.tool
```

Expected: `commit` matches. If not, clear-cache redeploy before diagnosing anything else — a stale
binary and a good one answer every other check identically.

- [ ] **Step 4: Success criterion 2 — a warm visit**

Load `https://aniko-agri.netlify.app/overview` twice; the second load is warm.
Expected: all five panels render real data in under a second, and **no waking message appears**.

- [ ] **Step 5: Success criterion 1 — a cold visit**

Wait ~15 minutes for the Render instance to spin down, then load the page with the network panel
open.
Expected: skeletons immediately, the waking message after ~3 seconds, then real data. **No error
state on any panel.** If a panel errors, check whether the total exceeded the 45s deadline — that
is the number to revisit, and the code comment records why it is 45.

- [ ] **Step 6: Success criterion 3 — the error path**

In DevTools, set the network to Offline and reload.
Expected: every panel shows an error with a **Try again** button. Restore the network, click
Retry on one panel, and it loads.

Then confirm the non-retryable branch by editing a request in DevTools to drop the `lng` parameter
from the suppliers call (or temporarily point `VITE_API_BASE_URL` at a local API and request a bad
range). Expected: an error with **no** Retry button.

- [ ] **Step 7: Success criterion 4 — the tiles are not zero**

Expected: all four tiles show non-zero values, and the trend chart's rightmost point is not at the
floor. This is what Part A bought.

- [ ] **Step 8: Report**

No commit. Report each of the seven success criteria from the spec as met or not met, with the
observed cold-start duration.

---

## Self-Review

**Spec coverage.** A1 → Tasks 1-3. A2 → Task 1. A3 → Task 12 (frontend ranges; the endpoint is
deliberately unchanged). A4 → Tasks 1-3, including the mutation check as Task 3 Step 4. B1 → the
layer order of Tasks 6-8 then 11-15. B2 → Task 6, with the CORS preflight split out as Task 5 so
it gates before any fetch code. B3 → Task 7. B4 → Task 8. B5 → Tasks 9-10. B6 → Tasks 11-15.
B7 → the hook rewrites in Tasks 11-15, with the broken promise recorded in each doc comment.
B8 → tests in every task plus Task 16. Risks → Task 4 Step 2 and Task 17 Step 3 (stale deploy),
Task 5 (CORS), Task 3 Step 4 (mutation check). Success criteria → Task 17.

**Two things to flag for the executor.**

1. **Task 12 Step 9 has a hazard the plan names but cannot fully pre-resolve.**
   `MarketPriceTrendsPanel` declares `config`, `tickLabel` and `tooltipLabel` between the hook call
   and the return, so where the early return goes determines whether it compiles. The step says so;
   read it before editing rather than after.

2. **Task 8's `adapt`-in-a-ref is the one place a well-meaning lint fix breaks production.**
   Adding `adapt` to the effect's dependency array produces an unbounded request loop against a
   service with a 22-second cold start. The comment in the source says this; the plan says it
   twice on purpose.

**Type consistency.** `ApiResource<T>`, `ApiFailure`, `Problem`, `ApiError`, `isRetryable`,
`apiFetch`, `QueryParams`, `installFetchStub`, `StubStep`, `adaptOverviewStats`,
`adaptPriceTrends`, `adaptNearbySuppliers`, `NearbySuppliersPayload`, `adaptFeaturedLots`,
`adaptRecentOrders`, `PanelState`, `StatTilesSkeleton`, `FeaturedLotsSkeleton`,
`RecentOrdersSkeleton`, `IDashboardClock.ReferenceNowAsync`, `DashboardClockCache.TryGet/Set`,
`IOrderRepository.LatestCreatedAtAsync`, `StubDashboardClock` — each is defined in exactly one task
and referenced with the same signature everywhere after it.
