# AniKo Dashboard Phase C — Stat Tiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put the four Buyer stat tiles on the Overview page, with deltas whose colour follows the meaning of the number rather than its sign.

**Architecture:** A fixture-backed `useOverviewStats` hook returns plain data — raw numbers, a format tag, a signed delta, and an `upIsGood` flag. `StatTile` renders one; `StatTilesRow` lays out four. Formatting goes through `lib/format.ts` so ₱ and thousands separators are locale-aware and defined once. Phase I swaps only the hook's adapter.

**Tech Stack:** React 19, TypeScript, Tailwind v4, lucide-react, i18next, `Intl.NumberFormat`, Vitest + Testing Library.

**Spec:** `plan/dashboard plan/aniko-dashboard.md` (Phase C), `plan/frontend plan/ui-ux.md`, `plan/dashboard plan/CHECKLIST.md`.

## Global Constraints

- **No raw hex.** `no-raw-hex.test.ts` enforces it. Delta colours come from `lib/chart-theme.ts`'s `DELTA`.
- Every user-facing string in **both** `en.json` and `fil.json`; `locales.test.ts` fails on drift.
- Minimum touch target 44px (`min-h-11`) for anything interactive. These tiles are not interactive — do not add a button role to them.
- The hook returns **raw numbers and a comparison value**, never a pre-formatted string. The API must not format either (backend checklist: "The API pre-formats nothing the frontend needs to localise").
- Baseline is **40 tests across 10 files**. Each task states the new total.

---

## Two spec-versus-mockup conflicts, resolved in favour of the spec

**1. The mockup shows the Farmer set; Phase C is the Buyer set.** The mockup's tiles are Active Listings / New Inquiries / Pending Orders / This Month Sales — but its own session chip reads "Juan Martinez, Buyer" and the Buy toggle is active. The spec assigns those four to **Phase H (Farmer)** and gives Phase C: **New Inquiries, Pending Orders, Saved Lots, Spend This Month**. The mockup is internally inconsistent; the spec is not. Build the Buyer set.

**2. The mockup is Californian and dollar-denominated.** `$48,760`, `Price (USD/kg)`, `$0.98 /kg`, and suppliers in San Jose, Salinas and Fresno. The spec and both checklists say Philippine regions and **₱/kg**, and the backend checklist requires "suppliers in real PH regions". The mockup was built on US placeholder data. Use **₱ and `en-PH`/`fil-PH`**.

Neither of these is a licence to redesign. Layout, spacing, the icon chip, the arrow-and-percent delta and the "vs last month" caption all come from the mockup exactly as drawn.

## The delta rule

This is the whole point of the phase, so it is worth stating precisely:

- The **arrow direction follows the sign** of the delta. Up is ↗, down is ↘.
- The **colour follows the meaning**, via each tile's `upIsGood` flag.

For a buyer: fewer Pending Orders means orders are being fulfilled, so a fall is good news and renders **green with a down arrow**. A rise in Spend This Month is not a success, so it renders **red with an up arrow**. Both combinations look wrong if you assume colour tracks the sign — that is the intended behaviour, and Task 3's tests pin it.

| Tile | `upIsGood` | Why |
|---|---|---|
| New Inquiries | `true` | More buying interest reaching the user |
| Pending Orders | `false` | Spec: "Pending Orders falling is good news for a buyer" |
| Saved Lots | `true` | A larger shortlist is more optionality |
| Spend This Month | `false` | Spec: "a rise in Spend is not a success" |

A **zero** delta is neither good nor bad. It renders muted with no arrow, and is not a rounding artefact to ignore — "unchanged" is real information on a dashboard.

---

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `frontend/src/lib/format.ts` | Locale-aware count, currency and percent formatting | 1 |
| `frontend/src/lib/format.test.ts` | Pins ₱, grouping, and the locale switch | 1 |
| `frontend/src/features/overview/types.ts` | `OverviewStat` — the contract Phase I must preserve | 2 |
| `frontend/src/features/overview/fixtures.ts` | The Buyer fixture set | 2 |
| `frontend/src/features/overview/useOverviewStats.ts` | Hook with a fixture adapter behind it | 2 |
| `frontend/src/features/overview/useOverviewStats.test.ts` | Shape and flags, independent of rendering | 2 |
| `frontend/src/features/overview/StatTile.tsx` | One tile: icon chip, label, value, delta | 3 |
| `frontend/src/features/overview/StatTile.test.tsx` | The delta rule, in all four combinations | 3 |
| `frontend/src/features/overview/StatTilesRow.tsx` | Responsive four-across layout | 4 |
| `frontend/src/features/overview/StatTilesRow.test.tsx` | Renders all four, in order | 4 |
| `frontend/src/app/routes/Overview.tsx` | Fills the `stats` slot | 4 |

---

## Task 1: Locale-aware formatting

**Files:**
- Create: `frontend/src/lib/format.ts`, `frontend/src/lib/format.test.ts`

**Interfaces:**
- Produces: `formatCount(value: number, locale: string): string`, `formatCurrency(value: number, locale: string): string`, `formatPercent(value: number, locale: string): string`

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from 'vitest'
import { formatCount, formatCurrency, formatPercent } from './format'

describe('formatCount', () => {
  it('groups thousands', () => {
    expect(formatCount(48760, 'en')).toBe('48,760')
  })

  it('leaves small numbers alone', () => {
    expect(formatCount(9, 'en')).toBe('9')
  })
})

describe('formatCurrency', () => {
  it('renders pesos, not dollars — the mockup\'s $ is US placeholder data', () => {
    expect(formatCurrency(48760, 'en')).toContain('₱')
    expect(formatCurrency(48760, 'en')).not.toContain('$')
  })

  it('groups thousands and shows no centavos', () => {
    expect(formatCurrency(48760, 'en')).toContain('48,760')
    expect(formatCurrency(48760, 'en')).not.toContain('.00')
  })

  it('still renders pesos under the Filipino locale', () => {
    expect(formatCurrency(48760, 'fil')).toContain('₱')
  })
})

describe('formatPercent', () => {
  it('renders the magnitude only — the arrow carries direction', () => {
    expect(formatPercent(12, 'en')).toBe('12%')
    expect(formatPercent(-2, 'en')).toBe('2%')
  })

  it('handles zero', () => {
    expect(formatPercent(0, 'en')).toBe('0%')
  })
})
```

- [ ] **Step 2: Run to verify failure**

`cd frontend && npm test -- --run format` → FAIL, cannot resolve `./format`.

- [ ] **Step 3: Implement**

```ts
/**
 * All number formatting for the dashboard, in one place and locale-aware.
 *
 * The app's i18n languages are `en` and `fil`; both are mapped to a PH region
 * because the product is Philippine regardless of the interface language. The
 * mockup's US dollars and Californian cities are placeholder data — the spec
 * and both checklists specify PH regions and pesos.
 */

const REGION: Record<string, string> = {
  en: 'en-PH',
  fil: 'fil-PH',
}

function resolve(locale: string): string {
  return REGION[locale] ?? REGION.en
}

/** Whole counts with thousands separators. */
export function formatCount(value: number, locale: string): string {
  return new Intl.NumberFormat(resolve(locale), { maximumFractionDigits: 0 }).format(value)
}

/**
 * Pesos, no centavos. Dashboard figures are read at a glance, and two decimal
 * places on a five-figure total is noise.
 */
export function formatCurrency(value: number, locale: string): string {
  return new Intl.NumberFormat(resolve(locale), {
    style: 'currency',
    currency: 'PHP',
    maximumFractionDigits: 0,
  }).format(value)
}

/**
 * Magnitude only — the caller renders an arrow for direction, so a minus sign
 * here would state it twice.
 */
export function formatPercent(value: number, locale: string): string {
  return `${formatCount(Math.abs(value), locale)}%`
}
```

- [ ] **Step 4: Run to verify pass** — `npm test -- --run format`, 7 tests. Suite total **47**.

If `formatCurrency` yields `PHP 48,760` rather than `₱48,760`, the runtime's ICU data is narrow. Do **not** hand-roll a `'₱' + formatCount(...)` fallback in that case without checking `npm run build` output in a browser first — Node's ICU and the browser's differ, and the browser is the one that ships.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/lib/format.ts frontend/src/lib/format.test.ts
git commit -m "feat(frontend): add locale-aware count, currency and percent formatting"
```

---

## Task 2: The stats hook and its fixture

**Files:**
- Create: `frontend/src/features/overview/types.ts`, `fixtures.ts`, `useOverviewStats.ts`, `useOverviewStats.test.ts`

**Interfaces:**
- Produces: `OverviewStat`, `useOverviewStats(): { stats: OverviewStat[]; isLoading: boolean }`

- [ ] **Step 1: Write the failing test**

```ts
import { renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { useOverviewStats } from './useOverviewStats'

describe('useOverviewStats', () => {
  it('returns the four Buyer tiles in mockup order', () => {
    const { result } = renderHook(() => useOverviewStats())
    expect(result.current.stats.map((s) => s.key)).toEqual([
      'new_inquiries',
      'pending_orders',
      'saved_lots',
      'spend_this_month',
    ])
  })

  it('marks the tiles where a rise is bad news', () => {
    const { result } = renderHook(() => useOverviewStats())
    const flags = Object.fromEntries(result.current.stats.map((s) => [s.key, s.upIsGood]))
    expect(flags).toEqual({
      new_inquiries: true,
      pending_orders: false,
      saved_lots: true,
      spend_this_month: false,
    })
  })

  it('returns raw numbers, never pre-formatted strings', () => {
    const { result } = renderHook(() => useOverviewStats())
    for (const stat of result.current.stats) {
      expect(typeof stat.value).toBe('number')
      expect(typeof stat.deltaPercent).toBe('number')
    }
  })

  it('carries a currency tag on the money tile only', () => {
    const { result } = renderHook(() => useOverviewStats())
    const currency = result.current.stats.filter((s) => s.format === 'currency')
    expect(currency.map((s) => s.key)).toEqual(['spend_this_month'])
  })
})
```

- [ ] **Step 2: Run to verify failure** — cannot resolve `./useOverviewStats`.

- [ ] **Step 3: Write `types.ts`**

```ts
import type { LucideIcon } from 'lucide-react'

/**
 * One stat tile's data. Deliberately raw: `value` and `deltaPercent` are
 * numbers, not strings, so the view layer can localise them. Phase I replaces
 * the adapter behind `useOverviewStats` and must preserve this shape.
 */
export interface OverviewStat {
  key: string
  labelKey: string
  icon: LucideIcon
  value: number
  format: 'count' | 'currency'
  /** Signed. The sign drives the arrow; `upIsGood` drives the colour. */
  deltaPercent: number
  /**
   * Whether a rise is good news. Read instead of the sign, because falling
   * Pending Orders is good for a buyer and rising Spend is not a success.
   */
  upIsGood: boolean
}
```

- [ ] **Step 4: Write `fixtures.ts`**

```ts
import { Bookmark, MessageSquare, ShoppingCart, Wallet } from 'lucide-react'
import type { OverviewStat } from './types'

/**
 * The Buyer set, per the spec's Phase C. The mockup shows the Farmer set
 * (Active Listings / Sales) even though its own session chip reads "Buyer" —
 * that is a mockup inconsistency, and the Farmer set is Phase H.
 *
 * Magnitudes are borrowed from the mockup so the layout is exercised at
 * realistic widths; the peso figure is scaled to a plausible PH value rather
 * than the mockup's US dollars.
 */
export const BUYER_STATS: OverviewStat[] = [
  {
    key: 'new_inquiries',
    labelKey: 'stats.new_inquiries',
    icon: MessageSquare,
    value: 16,
    format: 'count',
    deltaPercent: 6,
    upIsGood: true,
  },
  {
    key: 'pending_orders',
    labelKey: 'stats.pending_orders',
    icon: ShoppingCart,
    value: 9,
    format: 'count',
    deltaPercent: -2,
    upIsGood: false,
  },
  {
    key: 'saved_lots',
    labelKey: 'stats.saved_lots',
    icon: Bookmark,
    value: 28,
    format: 'count',
    deltaPercent: 12,
    upIsGood: true,
  },
  {
    key: 'spend_this_month',
    labelKey: 'stats.spend_this_month',
    icon: Wallet,
    value: 2_671_400,
    format: 'currency',
    deltaPercent: 18,
    upIsGood: false,
  },
]
```

- [ ] **Step 5: Write `useOverviewStats.ts`**

```ts
import { BUYER_STATS } from './fixtures'
import type { OverviewStat } from './types'

export interface OverviewStatsResult {
  stats: OverviewStat[]
  isLoading: boolean
}

/**
 * Phase C returns a fixture synchronously. Phase I swaps the body for a fetch
 * of `GET /api/v1/buyer/overview/stats` — the return shape must not change,
 * because every consumer is already written against `{ stats, isLoading }`.
 */
export function useOverviewStats(): OverviewStatsResult {
  return { stats: BUYER_STATS, isLoading: false }
}
```

- [ ] **Step 6: Run to verify pass** — 4 tests. Suite total **51**.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/features/overview
git commit -m "feat(frontend): add useOverviewStats with the Buyer fixture set"
```

---

## Task 3: The tile

**Files:**
- Create: `frontend/src/features/overview/StatTile.tsx`, `StatTile.test.tsx`
- Modify: `frontend/src/app/locales/en.json`, `frontend/src/app/locales/fil.json`

**Interfaces:**
- Consumes: `OverviewStat`, `formatCount`/`formatCurrency`/`formatPercent`, `DELTA` from `@/lib/chart-theme`
- Produces: `StatTile({ stat }: { stat: OverviewStat })`

- [ ] **Step 1: Add the strings to both catalogues (flat dotted keys)**

`en.json`:

```json
"stats.new_inquiries": "New Inquiries",
"stats.pending_orders": "Pending Orders",
"stats.saved_lots": "Saved Lots",
"stats.spend_this_month": "Spend This Month",
"stats.vs_last_month": "vs last month",
"stats.delta_up": "up {{percent}} versus last month",
"stats.delta_down": "down {{percent}} versus last month",
"stats.delta_flat": "unchanged versus last month",
```

`fil.json`:

```json
"stats.new_inquiries": "Mga Bagong Tanong",
"stats.pending_orders": "Mga Nakabinbing Order",
"stats.saved_lots": "Mga Naka-save na Lote",
"stats.spend_this_month": "Gastos Ngayong Buwan",
"stats.vs_last_month": "kumpara noong nakaraang buwan",
"stats.delta_up": "tumaas ng {{percent}} kumpara noong nakaraang buwan",
"stats.delta_down": "bumaba ng {{percent}} kumpara noong nakaraang buwan",
"stats.delta_flat": "walang pagbabago kumpara noong nakaraang buwan",
```

- [ ] **Step 2: Write the failing test**

The four sign×meaning combinations are the reason this component exists, so all four are pinned.

```tsx
import { render, screen } from '@testing-library/react'
import { ShoppingCart } from 'lucide-react'
import { describe, expect, it } from 'vitest'
import { DELTA } from '@/lib/chart-theme'
import type { OverviewStat } from './types'
import { StatTile } from './StatTile'

function stat(overrides: Partial<OverviewStat> = {}): OverviewStat {
  return {
    key: 'pending_orders',
    labelKey: 'stats.pending_orders',
    icon: ShoppingCart,
    value: 9,
    format: 'count',
    deltaPercent: 6,
    upIsGood: true,
    ...overrides,
  }
}

/** The rendered delta colour, read off the element's inline style. */
function deltaColour(): string {
  const el = screen.getByTestId('delta')
  return el.style.color
}

function rgb(hex: string): string {
  const n = hex.replace('#', '')
  const [r, g, b] = [0, 2, 4].map((i) => parseInt(n.slice(i, i + 2), 16))
  return `rgb(${r}, ${g}, ${b})`
}

describe('StatTile', () => {
  it('shows the label and the formatted value', () => {
    render(<StatTile stat={stat({ value: 48760 })} />)
    expect(screen.getByText('Pending Orders')).toBeInTheDocument()
    expect(screen.getByText('48,760')).toBeInTheDocument()
  })

  it('formats a currency tile in pesos', () => {
    render(<StatTile stat={stat({ format: 'currency', value: 2671400 })} />)
    expect(screen.getByTestId('value').textContent).toContain('₱')
  })

  // --- the delta rule: arrow follows the sign, colour follows the meaning ---

  it('a rise on an up-is-good tile is green and points up', () => {
    render(<StatTile stat={stat({ deltaPercent: 12, upIsGood: true })} />)
    expect(deltaColour()).toBe(rgb(DELTA.up))
    expect(screen.getByLabelText(/up 12% versus last month/i)).toBeInTheDocument()
  })

  it('a fall on an up-is-good tile is red and points down', () => {
    render(<StatTile stat={stat({ deltaPercent: -12, upIsGood: true })} />)
    expect(deltaColour()).toBe(rgb(DELTA.down))
    expect(screen.getByLabelText(/down 12% versus last month/i)).toBeInTheDocument()
  })

  it('a fall on an up-is-bad tile is GREEN, though it points down', () => {
    // Fewer pending orders means orders are being fulfilled. This is the case
    // that a sign-driven implementation gets wrong.
    render(<StatTile stat={stat({ deltaPercent: -2, upIsGood: false })} />)
    expect(deltaColour()).toBe(rgb(DELTA.up))
    expect(screen.getByLabelText(/down 2% versus last month/i)).toBeInTheDocument()
  })

  it('a rise on an up-is-bad tile is RED, though it points up', () => {
    // Spending more is not a success.
    render(<StatTile stat={stat({ deltaPercent: 18, upIsGood: false })} />)
    expect(deltaColour()).toBe(rgb(DELTA.down))
    expect(screen.getByLabelText(/up 18% versus last month/i)).toBeInTheDocument()
  })

  it('renders an unchanged delta as neither good nor bad', () => {
    render(<StatTile stat={stat({ deltaPercent: 0 })} />)
    expect(deltaColour()).toBe('')
    expect(screen.getByLabelText(/unchanged versus last month/i)).toBeInTheDocument()
  })

  it('states the comparison in words, because a bare arrow is a guess', () => {
    render(<StatTile stat={stat()} />)
    expect(screen.getByText(/vs last month/i)).toBeInTheDocument()
  })
})
```

- [ ] **Step 3: Run to verify failure** — cannot resolve `./StatTile`.

- [ ] **Step 4: Implement**

```tsx
import { useTranslation } from 'react-i18next'
import { ArrowDownRight, ArrowUpRight } from 'lucide-react'
import { DELTA } from '@/lib/chart-theme'
import { formatCount, formatCurrency, formatPercent } from '@/lib/format'
import type { OverviewStat } from './types'

/**
 * One dashboard stat. Not interactive: the mockup gives these no affordance,
 * and wrapping a figure in a button invents a destination that does not exist.
 */
export function StatTile({ stat }: { stat: OverviewStat }) {
  const { t, i18n } = useTranslation()
  const locale = i18n.language

  const value =
    stat.format === 'currency'
      ? formatCurrency(stat.value, locale)
      : formatCount(stat.value, locale)

  const percent = formatPercent(stat.deltaPercent, locale)
  const flat = stat.deltaPercent === 0
  const rose = stat.deltaPercent > 0

  // Colour follows the meaning, not the sign. A fall in Pending Orders is good
  // news for a buyer, and a rise in Spend is not a success — so `upIsGood` is
  // consulted here and the sign is used only for the arrow below.
  const isGoodNews = rose ? stat.upIsGood : !stat.upIsGood
  const colour = flat ? undefined : isGoodNews ? DELTA.up : DELTA.down

  const Arrow = rose ? ArrowUpRight : ArrowDownRight

  const deltaLabel = flat
    ? t('stats.delta_flat')
    : t(rose ? 'stats.delta_up' : 'stats.delta_down', { percent })

  return (
    <article className="rounded-xl bg-surface p-5">
      <div className="flex items-start gap-4">
        <span
          aria-hidden="true"
          className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-page"
        >
          <stat.icon className="size-6 text-primary" />
        </span>

        <div className="min-w-0">
          <p className="text-sm font-medium text-muted-fg">{t(stat.labelKey)}</p>
          <p data-testid="value" className="mt-1 text-3xl font-bold text-primary">
            {value}
          </p>
        </div>
      </div>

      <p className="mt-4 flex items-center gap-1 text-xs">
        {/*
          One accessible label on the whole delta rather than per-fragment: a
          screen reader announcing "up", "12%", "vs last month" as three
          separate nodes is worse than one sentence.
        */}
        <span
          data-testid="delta"
          aria-label={deltaLabel}
          style={{ color: colour }}
          className="flex items-center gap-0.5 font-semibold"
        >
          {!flat && <Arrow aria-hidden="true" className="size-3.5" />}
          {percent}
        </span>
        <span aria-hidden="true" className="text-muted-fg">
          {t('stats.vs_last_month')}
        </span>
      </p>
    </article>
  )
}
```

- [ ] **Step 5: Run to verify pass** — 8 tests. Suite total **59**.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/features/overview frontend/src/app/locales
git commit -m "feat(frontend): add StatTile with meaning-driven delta colour"
```

---

## Task 4: The row, on the page

**Files:**
- Create: `frontend/src/features/overview/StatTilesRow.tsx`, `StatTilesRow.test.tsx`
- Modify: `frontend/src/app/routes/Overview.tsx`

**Interfaces:**
- Consumes: `useOverviewStats`, `StatTile`
- Produces: `StatTilesRow()` — takes no props, owns its own data

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StatTilesRow } from './StatTilesRow'

describe('StatTilesRow', () => {
  it('renders every Buyer tile, in order', () => {
    render(<StatTilesRow />)
    const labels = screen.getAllByRole('article').map((el) => el.textContent)
    expect(labels).toHaveLength(4)
    expect(labels[0]).toContain('New Inquiries')
    expect(labels[3]).toContain('Spend This Month')
  })
})
```

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement**

```tsx
import { StatTile } from './StatTile'
import { useOverviewStats } from './useOverviewStats'

/**
 * Four across at `lg`, two at `md`, one below — per the checklist. The tiles
 * own no data of their own; the row fetches once and hands each tile a record.
 */
export function StatTilesRow() {
  const { stats } = useOverviewStats()

  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
      {stats.map((stat) => (
        <StatTile key={stat.key} stat={stat} />
      ))}
    </div>
  )
}
```

- [ ] **Step 4: Fill the slot in `Overview.tsx`**

Add the import, then replace the self-closing stats section:

```tsx
import { StatTilesRow } from '@/features/overview/StatTilesRow'
```

```tsx
<section data-slot="stats" className="col-span-full">
  <StatTilesRow />
</section>
```

Leave the comment block's remaining Phase D–G lines intact, and drop only the `stats` line from it.

- [ ] **Step 5: Run everything**

```bash
cd frontend && npm test -- --run && npm run build && npm run lint
```

Expected: PASS, **60 tests**; build and lint clean apart from the two pre-existing shadcn `only-export-components` warnings.

- [ ] **Step 6: Commit**

```bash
git add frontend/src
git commit -m "feat(frontend): render the stat tiles row on Overview"
```

---

## Definition of done

- Four Buyer tiles on `/overview`: icon chip, label, value, signed delta, "vs last month"
- Delta colour reads `upIsGood`, not the sign — all four combinations pinned by tests, plus zero
- ₱ and thousands separators via `lib/format.ts`, correct under both `en` and `fil`
- Four across at `lg`, two at `md`, one below
- `useOverviewStats` returns raw numbers; no formatting crosses the hook boundary
- 60 tests green, build and lint clean
- Phase C ticked in `plan/dashboard plan/CHECKLIST.md`, with both mockup conflicts recorded

## Explicitly not in this plan

- The Farmer stat set — Phase H, and it needs a Farmer mockup first
- Any fetch. `useOverviewStats` stays fixture-backed until Phase I
- Loading and empty states. The hook returns `isLoading: false` synchronously; skeletons arrive with the real fetch, where they can be tested against a delay that actually exists
- Making tiles clickable. The mockup gives them no affordance
