# AniKo Dashboard Phase D — Market Price Trends Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Market Price Trends panel — three crop price series in ₱/kg on a Recharts line chart, with a working range selector, a legend above the plot and a source caption — and fill the `pricing` slot on Overview.

**Architecture:** A new `features/pricing/` slice mirroring `features/overview/`: a deterministic fixture, a `useMarketPriceTrends(months)` hook that slices it, and a `MarketPriceTrendsPanel` that renders it through shadcn's `ChartContainer`. Colour reaches the chart only through `ChartConfig`, fed from `SERIES` in `lib/chart-theme.ts`, so `no-raw-hex.test.ts` stays green.

**Tech Stack:** React 19.2, TypeScript, Recharts 3.10.1, shadcn `chart` (base-nova), Tailwind v4, i18next, Vitest + Testing Library.

**Spec:** `plan/dashboard plan/aniko-dashboard.md` (Phase D, lines 173–184) and `plan/dashboard plan/CHECKLIST.md` (Phase D).

## Global Constraints

- No raw hex outside `lib/chart-theme.ts`, `lib/color.ts` and their tests — enforced by `no-raw-hex.test.ts`.
- All user-visible copy goes through i18next, using **flat dotted keys** (`"pricing.title"`), added to **both** `en.json` and `fil.json`.
- Components reference token *names*, never primitive values.
- Currency is the Philippine peso. The mockup's US dollars and Californian place names are mockup errors; the spec wins.
- Single linear git history on `main`. Commit per task.
- `plan/` is gitignored — checklist edits stay local and are never committed.

---

## Research findings that constrain this plan

These were measured in this repo against the installed versions, not assumed. Do not re-litigate them.

**1. `ResponsiveContainer` renders nothing under jsdom — silently.**
jsdom has no `ResizeObserver`. Recharts 3 guards on that and returns early rather than throwing, so a bare
`<ResponsiveContainer width="100%">` produces `<div style="width: 0px">` with **no SVG and no error**. A test
asserting "the panel rendered" passes green with no plot in it.

**2. Adding a `ResizeObserver` mock makes it worse.** The most-copied workaround — a no-op `ResizeObserver`
class in `src/test/setup.ts` — re-enables Recharts' measuring effect, which calls `getBoundingClientRect()`
(0×0 in jsdom) and overwrites the positive seed dimension. **Do not add one**, and leave the warning comment
this plan adds to `setup.ts` in place.

**3. shadcn's `ChartContainer` already solves it.** It passes `initialDimension={{width: 320, height: 200}}`
to `ResponsiveContainer`. With no `ResizeObserver` present, Recharts keeps that seed and renders. Measured
against this repo's `recharts@3.10.1` + `jsdom@30`: `svg present: true`, `.recharts-line-curve` × 2,
`.recharts-dot` × 6, tick text `Dec|Jan|Feb|0|15|30|45|60`. No `act()` warnings. **No test-side mocking of any
kind is required.**

**4. `ChartContainer` injects colour as CSS variables.** Given `config = { rice: { label, color: '#3A942A' } }`
it emits `[data-chart=…] { --color-rice: #3A942A; }`. Series therefore use `stroke="var(--color-rice)"`, and
the only hex in the tree arrives from `chart-theme.ts` through the config object.

**5. The shadcn `chart` component is already installed.** `npx shadcn add chart` fails in this environment
(npm `EALLOWSCRIPTS` policy blocks its `npm install recharts@3.8.0` sub-step), so `src/components/ui/chart.tsx`
was written from the registry payload directly and its internal alias rewritten to `@/lib/utils`. It contains
no raw hex and compiles against 3.10.1. **`recharts` stays at `^3.10.1`; do not accept the registry's 3.8.0 pin.**

**6. Do not assert on SVG geometry.** Path `d` values, coordinates and tick positions are Recharts internals.
Assert chrome (title, legend, selector, caption) plus **one** structural guard that the plot exists at all.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/features/pricing/types.ts` | `PricePoint`, `RangeMonths`, `MarketPriceTrendsResult` |
| `src/features/pricing/fixtures.ts` | 52 deterministic weekly points; no RNG |
| `src/features/pricing/useMarketPriceTrends.ts` | Slices the fixture by range |
| `src/features/pricing/MarketPriceTrendsPanel.tsx` | Card chrome, legend, chart, caption |
| `src/lib/chart-theme.ts` | *modify* — add `SERIES_FILL` for the Rice area wash |
| `src/test/setup.ts` | *modify* — add the "never add a ResizeObserver mock" warning |
| `src/app/locales/{en,fil}.json` | *modify* — `pricing.*` keys |
| `src/app/routes/Overview.tsx` | *modify* — fill the `pricing` slot |

---

### Task 1: The fixture and its contract

**Files:**
- Create: `frontend/src/features/pricing/types.ts`, `frontend/src/features/pricing/fixtures.ts`
- Test: `frontend/src/features/pricing/fixtures.test.ts`
- Modify: `frontend/src/lib/chart-theme.ts`, `frontend/src/test/setup.ts`

**Interfaces:**
- Consumes: `SeriesKey` from `@/lib/chart-theme`
- Produces: `PricePoint`, `RangeMonths`, `RANGE_MONTHS`, `WEEKS_PER_RANGE`, `WEEKLY_PRICES`

- [ ] **Step 1: Write the failing test** — `fixtures.test.ts`

```ts
import { describe, expect, it } from 'vitest'
import { WEEKLY_PRICES } from './fixtures'

describe('WEEKLY_PRICES', () => {
  it('holds twelve months of weekly points', () => {
    expect(WEEKLY_PRICES).toHaveLength(52)
  })

  it('is ordered oldest first', () => {
    const dates = WEEKLY_PRICES.map((p) => p.date)
    expect([...dates].sort()).toEqual(dates)
  })

  it('is deterministic — no RNG, so the plot never changes between runs', () => {
    expect(WEEKLY_PRICES[0]).toEqual({
      date: '2025-08-23', rice: 48, corn: 32, vegetables: 17,
    })
  })

  // The mockup draws three cleanly separated bands. If the generated series
  // ever overlap, the chart stops being readable and the palette work in
  // Phase B is wasted — so the separation is asserted, not eyeballed.
  it('keeps the three crops in non-overlapping price bands', () => {
    const band = (key: 'rice' | 'corn' | 'vegetables') => {
      const v = WEEKLY_PRICES.map((p) => p[key])
      return { min: Math.min(...v), max: Math.max(...v) }
    }
    expect(band('vegetables').max).toBeLessThan(band('corn').min)
    expect(band('corn').max).toBeLessThan(band('rice').min)
  })

  it('quotes plausible Philippine peso prices per kilo', () => {
    for (const point of WEEKLY_PRICES) {
      expect(point.rice).toBeGreaterThan(10)
      expect(point.rice).toBeLessThan(200)
    }
  })
})
```

- [ ] **Step 2: Run it and watch it fail**

Run: `npx vitest run src/features/pricing/fixtures.test.ts`
Expected: FAIL — cannot resolve `./fixtures`.

- [ ] **Step 3: Write `types.ts`**

```ts
import type { SeriesKey } from '@/lib/chart-theme'

/** One week's closing price for every crop, in ₱/kg. Shaped for Recharts:
 *  one row per x-value, one key per series. */
export type PricePoint = { date: string } & Record<SeriesKey, number>

export const RANGE_MONTHS = [3, 6, 12] as const
export type RangeMonths = (typeof RANGE_MONTHS)[number]

export interface MarketPriceTrendsResult {
  points: PricePoint[]
  isLoading: boolean
}
```

- [ ] **Step 4: Write `fixtures.ts`**

```ts
import type { PricePoint, RangeMonths } from './types'

/**
 * Twelve months of weekly prices, generated rather than hand-typed — 156
 * numbers by hand is a transcription-error factory.
 *
 * The walk is a sum of two sines, NOT `Math.random`. Random fixtures give a
 * plot that changes on every run, which makes a visual regression impossible
 * to see and a snapshot impossible to keep. This is deterministic, so
 * `fixtures.test.ts` can assert exact values.
 *
 * Bands are chosen so the three crops never cross — see the mockup, where the
 * separation is what makes three series legible at once.
 */
const WEEKS = 52

/** The Saturday the most recent week closes on. A constant, not `new Date()`:
 *  a fixture that drifts with the clock makes tests fail on a future date. */
const LATEST_WEEK_END = '2026-08-15'

const CROPS = {
  rice: { start: 48.0, end: 62.0, amplitude: 1.6, k1: 0.55, k2: 0.23 },
  corn: { start: 32.0, end: 36.0, amplitude: 0.9, k1: 0.41, k2: 0.17 },
  vegetables: { start: 17.0, end: 19.2, amplitude: 0.7, k1: 0.67, k2: 0.31 },
} as const

function priceAt(crop: (typeof CROPS)[keyof typeof CROPS], week: number): number {
  const progress = week / (WEEKS - 1)
  const trend = crop.start + (crop.end - crop.start) * progress
  const wobble =
    crop.amplitude * (Math.sin(week * crop.k1) * 0.6 + Math.sin(week * crop.k2) * 0.4)
  return Math.round((trend + wobble) * 100) / 100
}

function weekEnding(index: number): string {
  const latest = new Date(`${LATEST_WEEK_END}T00:00:00Z`)
  const day = new Date(latest)
  day.setUTCDate(latest.getUTCDate() - (WEEKS - 1 - index) * 7)
  return day.toISOString().slice(0, 10)
}

export const WEEKLY_PRICES: PricePoint[] = Array.from({ length: WEEKS }, (_, i) => ({
  date: weekEnding(i),
  rice: priceAt(CROPS.rice, i),
  corn: priceAt(CROPS.corn, i),
  vegetables: priceAt(CROPS.vegetables, i),
}))

/** Exact week counts rather than `months * 4.345` rounded — a range selector
 *  that returns 13 points for one user and 14 for another is a bug report. */
export const WEEKS_PER_RANGE: Record<RangeMonths, number> = { 3: 13, 6: 26, 12: 52 }
```

- [ ] **Step 5: Add `SERIES_FILL` to `chart-theme.ts`**

Append below `SERIES`:

```ts
/**
 * The mockup washes a pale green under the Rice line only. It is decoration,
 * not data — it carries no value a reader could misread — so it is deliberately
 * far too light to be mistaken for a fourth series, and no contrast threshold
 * applies to it.
 */
export const SERIES_FILL = {
  rice: '#EAF5E6',
} as const
```

- [ ] **Step 6: Add the warning to `src/test/setup.ts`**

Append:

```ts
/*
 * DO NOT add a `ResizeObserver` polyfill or mock here.
 *
 * jsdom has none, and Recharts 3 treats its absence as "skip measuring and keep
 * the seed dimension" — which is why charts render in these tests at all.
 * Adding a no-op mock re-enables the measuring effect, which then reads 0x0
 * from `getBoundingClientRect` and overwrites the seed. The chart renders
 * empty, no error is raised, and nothing points back at this file.
 *
 * Measured against recharts@3.10.1 + jsdom@30. See
 * docs/superpowers/plans/2026-08-16-aniko-dashboard-phase-d.md.
 */
```

- [ ] **Step 7: Run the tests**

Run: `npx vitest run src/features/pricing/fixtures.test.ts`
Expected: PASS, 5 tests. If the determinism assertion fails, print the real first point and update the
expected literal — do not loosen the assertion to `expect.any(Number)`.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/features/pricing frontend/src/lib/chart-theme.ts frontend/src/test/setup.ts
git commit -m "feat(frontend): add the deterministic weekly price fixture"
```

---

### Task 2: `useMarketPriceTrends(months)`

**Files:**
- Create: `frontend/src/features/pricing/useMarketPriceTrends.ts`
- Test: `frontend/src/features/pricing/useMarketPriceTrends.test.ts`

**Interfaces:**
- Consumes: `WEEKLY_PRICES`, `WEEKS_PER_RANGE`, `RangeMonths`, `MarketPriceTrendsResult`
- Produces: `useMarketPriceTrends(months: RangeMonths): MarketPriceTrendsResult`

- [ ] **Step 1: Write the failing test**

```ts
import { renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { WEEKLY_PRICES } from './fixtures'
import { useMarketPriceTrends } from './useMarketPriceTrends'

describe('useMarketPriceTrends', () => {
  it.each([
    [3, 13],
    [6, 26],
    [12, 52],
  ] as const)('returns %i months as %i weekly points', (months, expected) => {
    const { result } = renderHook(() => useMarketPriceTrends(months))
    expect(result.current.points).toHaveLength(expected)
  })

  // The range selector is real, not decorative — the spec is explicit. If every
  // range returned the same slice the control would look functional and do
  // nothing, which is worse than omitting it.
  it('returns a genuinely different slice per range', () => {
    const three = renderHook(() => useMarketPriceTrends(3)).result.current.points
    const twelve = renderHook(() => useMarketPriceTrends(12)).result.current.points
    expect(three[0].date).not.toBe(twelve[0].date)
  })

  it('always ends at the most recent week, whatever the range', () => {
    const latest = WEEKLY_PRICES[WEEKLY_PRICES.length - 1]
    for (const months of [3, 6, 12] as const) {
      const { result } = renderHook(() => useMarketPriceTrends(months))
      expect(result.current.points.at(-1)).toEqual(latest)
    }
  })

  it('reports a settled state, since the fixture adapter is synchronous', () => {
    const { result } = renderHook(() => useMarketPriceTrends(6))
    expect(result.current.isLoading).toBe(false)
  })
})
```

- [ ] **Step 2: Run it and watch it fail**

Run: `npx vitest run src/features/pricing/useMarketPriceTrends.test.ts`
Expected: FAIL — cannot resolve `./useMarketPriceTrends`.

- [ ] **Step 3: Implement**

```ts
import { useMemo } from 'react'
import { WEEKLY_PRICES, WEEKS_PER_RANGE } from './fixtures'
import type { MarketPriceTrendsResult, RangeMonths } from './types'

/**
 * Phase I swaps the body for `GET /api/v1/pricing/trends?months=`. The
 * signature is the contract and does not change when that happens — which is
 * why the panel never touches the fixture directly.
 */
export function useMarketPriceTrends(months: RangeMonths): MarketPriceTrendsResult {
  const points = useMemo(() => WEEKLY_PRICES.slice(-WEEKS_PER_RANGE[months]), [months])
  return { points, isLoading: false }
}
```

- [ ] **Step 4: Run the tests**

Run: `npx vitest run src/features/pricing/useMarketPriceTrends.test.ts`
Expected: PASS, 6 tests.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/features/pricing
git commit -m "feat(frontend): add useMarketPriceTrends with a real range slice"
```

---

### Task 3: `MarketPriceTrendsPanel`

**Files:**
- Create: `frontend/src/features/pricing/MarketPriceTrendsPanel.tsx`
- Test: `frontend/src/features/pricing/MarketPriceTrendsPanel.test.tsx`
- Modify: `frontend/src/app/locales/en.json`, `frontend/src/app/locales/fil.json`

**Interfaces:**
- Consumes: `useMarketPriceTrends`, `RANGE_MONTHS`, `SERIES`, `SERIES_FILL`, `formatCurrency`
- Produces: `MarketPriceTrendsPanel` (no props)

- [ ] **Step 1: Add the locale keys**

To `en.json`:

```json
  "pricing.title": "Market Price Trends",
  "pricing.range_label": "Price history range",
  "pricing.range_3": "Last 3 Months",
  "pricing.range_6": "Last 6 Months",
  "pricing.range_12": "Last 12 Months",
  "pricing.axis_label": "Price (₱/kg)",
  "pricing.rice": "Rice (White)",
  "pricing.corn": "Corn (Yellow)",
  "pricing.vegetables": "Vegetables (Mixed)",
  "pricing.source": "Source: AniKo Market Data",
  "pricing.chart_label": "Weekly wholesale price per kilo for rice, corn and vegetables over the selected range.",
```

To `fil.json`:

```json
  "pricing.title": "Takbo ng Presyo sa Merkado",
  "pricing.range_label": "Saklaw ng kasaysayan ng presyo",
  "pricing.range_3": "Huling 3 Buwan",
  "pricing.range_6": "Huling 6 na Buwan",
  "pricing.range_12": "Huling 12 Buwan",
  "pricing.axis_label": "Presyo (₱/kg)",
  "pricing.rice": "Bigas (Puti)",
  "pricing.corn": "Mais (Dilaw)",
  "pricing.vegetables": "Gulay (Halo-halo)",
  "pricing.source": "Pinagmulan: AniKo Market Data",
  "pricing.chart_label": "Lingguhang presyo bawat kilo ng bigas, mais at gulay sa napiling saklaw.",
```

- [ ] **Step 2: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { MarketPriceTrendsPanel } from './MarketPriceTrendsPanel'

describe('MarketPriceTrendsPanel', () => {
  it('names itself and cites its source', () => {
    render(<MarketPriceTrendsPanel />)
    expect(screen.getByText('Market Price Trends')).toBeInTheDocument()
    expect(screen.getByText('Source: AniKo Market Data')).toBeInTheDocument()
  })

  it('legends all three crops above the plot', () => {
    render(<MarketPriceTrendsPanel />)
    const legend = screen.getByTestId('legend')
    for (const crop of ['Rice (White)', 'Corn (Yellow)', 'Vegetables (Mixed)']) {
      expect(legend).toHaveTextContent(crop)
    }
  })

  /*
   * THE GUARD. Recharts renders NOTHING under jsdom when the container measures
   * zero, and it does so silently — no throw, no warning. Every other assertion
   * in this file passes against an empty plot. This one fails.
   *
   * `.recharts-line-curve` is a Recharts styling hook rather than a public API,
   * so it is used once, here, and deliberately not relied on anywhere else.
   */
  it('actually draws three series, rather than an empty container', () => {
    const { container } = render(<MarketPriceTrendsPanel />)
    expect(container.querySelectorAll('.recharts-line-curve')).toHaveLength(3)
  })

  it('defaults to six months, as the mockup shows', () => {
    render(<MarketPriceTrendsPanel />)
    expect(screen.getByRole('combobox', { name: /price history range/i })).toHaveValue('6')
  })

  it('redraws when the range changes', async () => {
    const user = userEvent.setup()
    const { container } = render(<MarketPriceTrendsPanel />)
    const dots = () => container.querySelectorAll('.recharts-dot').length

    const before = dots()
    await user.selectOptions(
      screen.getByRole('combobox', { name: /price history range/i }),
      '3',
    )
    expect(dots()).toBeLessThan(before)
  })

  it('describes the plot for anyone who cannot see it', () => {
    render(<MarketPriceTrendsPanel />)
    expect(screen.getByRole('img', { name: /weekly wholesale price per kilo/i })).toBeInTheDocument()
  })
})
```

- [ ] **Step 3: Run it and watch it fail**

Run: `npx vitest run src/features/pricing/MarketPriceTrendsPanel.test.tsx`
Expected: FAIL — cannot resolve `./MarketPriceTrendsPanel`.

- [ ] **Step 4: Implement**

```tsx
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CartesianGrid, Area, ComposedChart, Line, Tooltip, XAxis, YAxis } from 'recharts'
import { ChartContainer, type ChartConfig } from '@/components/ui/chart'
import { SERIES, SERIES_FILL } from '@/lib/chart-theme'
import { formatCurrency } from '@/lib/format'
import { useMarketPriceTrends } from './useMarketPriceTrends'
import { RANGE_MONTHS, type RangeMonths } from './types'

/*
 * Colour enters the chart ONLY through this config. `ChartContainer` turns it
 * into `--color-rice` / `--color-corn` / `--color-vegetables` CSS variables, so
 * the series below reference `var(--color-*)` and never a literal — which is
 * what keeps `no-raw-hex.test.ts` green while the hexes stay in chart-theme.ts.
 */
const CROPS = ['rice', 'corn', 'vegetables'] as const

export function MarketPriceTrendsPanel() {
  const { t, i18n } = useTranslation()
  const [months, setMonths] = useState<RangeMonths>(6)
  const { points } = useMarketPriceTrends(months)

  const config = {
    rice: { label: t('pricing.rice'), color: SERIES.rice },
    corn: { label: t('pricing.corn'), color: SERIES.corn },
    vegetables: { label: t('pricing.vegetables'), color: SERIES.vegetables },
  } satisfies ChartConfig

  // Month-and-year, not the raw ISO date: 52 full dates on one axis is noise.
  const tickLabel = (iso: string) =>
    new Date(`${iso}T00:00:00Z`).toLocaleDateString(i18n.language, {
      month: 'short',
      timeZone: 'UTC',
    })

  return (
    <div className="rounded-xl bg-surface p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-bold text-primary">{t('pricing.title')}</h2>

        <label className="sr-only" htmlFor="pricing-range">
          {t('pricing.range_label')}
        </label>
        <select
          id="pricing-range"
          value={months}
          onChange={(e) => setMonths(Number(e.target.value) as RangeMonths)}
          className="rounded-lg border border-border bg-surface px-3 py-1.5 text-sm text-primary"
        >
          {RANGE_MONTHS.map((m) => (
            <option key={m} value={m}>
              {t(`pricing.range_${m}`)}
            </option>
          ))}
        </select>
      </div>

      {/* Legend above the plot, as real DOM rather than Recharts' <Legend>:
          it is readable by a screen reader and assertable without touching SVG. */}
      <ul data-testid="legend" className="mt-4 flex flex-wrap gap-x-6 gap-y-2">
        {CROPS.map((crop) => (
          <li key={crop} className="flex items-center gap-2 text-sm text-primary">
            <span
              aria-hidden="true"
              className="h-0.5 w-4 rounded-full"
              style={{ backgroundColor: SERIES[crop] }}
            />
            {config[crop].label}
          </li>
        ))}
      </ul>

      <p className="mt-4 text-xs text-muted-fg">{t('pricing.axis_label')}</p>

      <ChartContainer
        config={config}
        role="img"
        aria-label={t('pricing.chart_label')}
        className="mt-1 h-[280px] w-full"
      >
        <ComposedChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
          <CartesianGrid vertical={false} stroke="var(--color-border)" />
          <XAxis
            dataKey="date"
            tickFormatter={tickLabel}
            tickLine={false}
            axisLine={false}
            minTickGap={24}
            tick={{ fill: 'var(--color-muted-fg)', fontSize: 12 }}
          />
          <YAxis
            tickLine={false}
            axisLine={false}
            width={44}
            tick={{ fill: 'var(--color-muted-fg)', fontSize: 12 }}
          />
          <Tooltip
            formatter={(value, name) => [
              formatCurrency(Number(value), i18n.language),
              config[name as (typeof CROPS)[number]]?.label ?? name,
            ]}
            labelFormatter={tickLabel}
          />
          {/* Decoration only — see SERIES_FILL in chart-theme.ts. */}
          <Area
            dataKey="rice"
            stroke="none"
            fill={SERIES_FILL.rice}
            isAnimationActive={false}
          />
          {CROPS.map((crop) => (
            <Line
              key={crop}
              dataKey={crop}
              stroke={`var(--color-${crop})`}
              strokeWidth={2}
              dot={{ r: 2.5 }}
              isAnimationActive={false}
            />
          ))}
        </ComposedChart>
      </ChartContainer>

      <p className="mt-3 text-center text-xs text-muted-fg">{t('pricing.source')}</p>
    </div>
  )
}
```

- [ ] **Step 5: Run the tests**

Run: `npx vitest run src/features/pricing/MarketPriceTrendsPanel.test.tsx`
Expected: PASS, 7 tests.

If the three-series guard fails, the plot is not rendering — re-read finding 1–3 above before changing the
test. The fix is never to weaken the assertion.

- [ ] **Step 6: Mutation-test the guard**

Temporarily replace `className="mt-1 h-[280px] w-full"` with `className="mt-1"` on `ChartContainer`, or drop
one `<Line>`, and confirm the three-series test fails. Restore. A guard that cannot fail is not a guard —
this is the same discipline applied to `no-raw-hex.test.ts` in Phase B.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/features/pricing frontend/src/app/locales
git commit -m "feat(frontend): add the Market Price Trends panel"
```

---

### Task 4: On the page

**Files:**
- Modify: `frontend/src/app/routes/Overview.tsx`
- Test: `frontend/src/app/routes/Overview.test.tsx` (if present; otherwise none)

- [ ] **Step 1: Wire it in**

Add the import:

```tsx
import { MarketPriceTrendsPanel } from '@/features/pricing/MarketPriceTrendsPanel'
```

Fill the slot:

```tsx
<section data-slot="pricing" className="col-span-full lg:col-span-8">
  <MarketPriceTrendsPanel />
</section>
```

Update the comment block so `pricing` is no longer listed as pending.

- [ ] **Step 2: Full verification**

Run: `npm test -- --run && npm run build && npm run lint`
Expected: **78 tests** (60 + 5 fixture + 6 hook + 7 panel), build clean, lint showing only the two
pre-existing shadcn `only-export-components` warnings — plus any the new `chart.tsx` adds, which are
acceptable in a vendored registry file.

- [ ] **Step 3: Confirm `recharts` was not downgraded**

Run: `git diff frontend/package.json`
Expected: `"recharts": "^3.10.1"`. If it reads `3.8.0`, restore it — see finding 5.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/app/routes/Overview.tsx frontend/package.json frontend/package-lock.json frontend/src/components/ui/chart.tsx
git commit -m "feat(frontend): render Market Price Trends on Overview"
```

---

## Self-Review

**Spec coverage.** Phase D asks for: `MarketPriceTrendsPanel` via Recharts through shadcn's `chart` wrapper
(Task 3 ✓), three line series with point markers (Task 3, `dot={{ r: 2.5 }}` ✓), a range selector (Task 3 ✓),
legend above the plot (Task 3 ✓), the source caption (Task 3 ✓), `useMarketPriceTrends(months)` returning a
dated series per crop in ₱/kg (Task 2 ✓), a mock holding twelve months so the range visibly changes
(Task 1, 52 weeks; asserted in Task 2 ✓).

**Known deviations, both deliberate:**
1. *Native `<select>` rather than a styled dropdown.* The mockup draws a custom chevron control. A native
   select is keyboard- and screen-reader-correct for free, and `userEvent.selectOptions` tests it without a
   portal. Revisit only if the visual gap matters more than the a11y it would cost.
2. *Own legend markup instead of Recharts' `<Legend>`.* The spec says "legend above the plot"; Recharts
   renders its legend inside the SVG, where it is neither above the plot nor reliably readable by AT.

**Type consistency.** `RangeMonths` is defined in `types.ts` and used identically in `fixtures.ts`,
`useMarketPriceTrends.ts` and the panel's `useState<RangeMonths>`. `PricePoint` keys are `SeriesKey`, which is
what `CROPS` iterates and what `ChartConfig` is keyed by — so a new crop is added in exactly one place,
`SERIES`, and the compiler finds the rest.

**Not in scope.** The Farmer variant (Phase H), the real endpoint (Phase I), and the 360px walkthrough.
