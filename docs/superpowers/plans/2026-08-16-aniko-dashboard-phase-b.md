# AniKo Dashboard Phase B — Mobile Navigation, Tokens & Chart Theme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the dashboard working navigation on a phone, then replace every placeholder colour with values derived from AniKo's own brand assets and validated by a perceptual check that runs as a test.

**Architecture:** Two independent halves. First, `DashboardShell` gains a Base UI drawer so the sidebar has a mobile equivalent, and the header becomes sticky. Second, the three-layer token set in `index.css` gets real values measured from the logo and mockup, and `lib/chart-theme.ts` becomes the single home for every series and status colour. A `palette.test.ts` enforces the contrast and perceptual-distance thresholds so a future edit that breaks them fails CI instead of shipping.

**Tech Stack:** React 19, TypeScript, Tailwind v4 (`@theme`), Base UI 1.7.0 (`@base-ui/react/drawer`), lucide-react, Vitest + Testing Library.

**Spec:** `plan/dashboard plan/aniko-dashboard.md` (Phase B) and `plan/dashboard plan/CHECKLIST.md`.

## Global Constraints

- **No raw hex in any component.** Colour comes from a token or from `lib/chart-theme.ts`. Task 5 makes this a test.
- **Token flow is one-directional:** Primitive → Semantic → Component. A semantic token may not reference another semantic token; a component token may not reference a primitive directly.
- Every value in `index.css` is currently a **placeholder** (see the Phase A note in that file). Task 3 replaces the values; component code references names and must not move.
- **Do not redefine `--color-accent`** to satisfy shadcn's hover convention. The collision is documented in `index.css` and ours wins.
- Base UI is already a dependency at **1.7.0**. Do not add Radix, vaul, or any other dialog/drawer library.
- Minimum touch target is **44px** (`min-h-11`), already the convention in `Header.tsx` and `Sidebar.tsx`.
- Every interactive element needs an accessible name. Icon-only buttons take `aria-label` from the i18n catalogue, never a bare string.
- Any new user-facing string must be added to **both** `en.json` and `fil.json`. `locales.test.ts` fails on key drift.
- Tests run with `npm test` from `frontend/`. Phase A ends at **16 tests across 6 files**; every task below states the expected new total.

---

## Measured inputs

These were sampled from the brand assets with Pillow, not eyeballed. They are the inputs to Tasks 3 and 4 and should not be re-derived.

**From `plan/AniKo assets/aniko logo.png`:**

| Role | Hex | Share of logo |
|---|---|---|
| Deep green (wordmark, dark leaf) | `#004824` | 55.7% |
| Gold (wheat, arrow) | `#F0A800` | 16.2% |
| Leaf green (light leaf) | `#54A818` | 16.2% |

**From `plan/dashboard plan/AniKo dashboard mockup image.png`:**

| Role | Hex |
|---|---|
| Chrome dark green (sidebar active, CTA, Buy toggle) | `#023C16` |
| Page background (warm off-white) | `#FBFAF8` |
| Sidebar background | `#FCFAF7` |
| Card surface | `#FDFDFD` |
| Chart — Rice (White) | `#3A942A` |
| Chart — Corn (Yellow) | `#FCAE11` |
| Chart — Vegetables (Mixed) | `#09410E` |
| Delta up | `#2D753A` |
| Delta down | `#F4481A` |
| Badge Confirmed — text / fill | `#2C4D38` / `#E9F3E5` |
| Badge Processing — text / fill | `#876439` / `#FDF3DF` |
| Badge Shipped — text / fill | `#305593` / `#E3EFFE` |
| Badge Delivered — text / fill | `#2F573C` / `#E5F3E3` |

## What the perceptual check already found

The check described in the spec has been run against the mockup's own hexes. Three results matter, and two of them contradict what the spec expected.

**1. The three chart series pass, comfortably.** The spec predicted that "two greens and an amber in one line chart is exactly the arrangement that fails a perceptual-distance check." Measured CIEDE2000:

| Pair | ΔE2000 | Verdict (threshold 20) |
|---|---|---|
| Rice vs Corn | 41.90 | PASS |
| Rice vs Vegetables | 28.09 | PASS |
| Corn vs Vegetables | 63.71 | PASS |

The designer separated the two greens by lightness far more than the description suggested. **Do not "fix" the series hues.** The CCTMS lesson cuts both ways — a confident wrong answer is as easy to produce by assuming failure as by assuming success.

**2. The real chart defect is contrast, not separation.** Against the white plot area:

| Series | Contrast vs `#FFFFFF` | WCAG 1.4.11 (3:1 for graphics) |
|---|---|---|
| Vegetables `#09410E` | 11.84:1 | PASS |
| Rice `#3A942A` | 3.85:1 | PASS |
| **Corn `#FCAE11`** | **1.87:1** | **FAIL** |

A 1.87:1 line is close to invisible for a low-vision user and washes out completely on a phone in daylight — which is the expected reading condition for a farmer in the field. Snapping lightness down while holding hue (40°) and saturation (0.98) constant gives **`#C88703` at 3.03:1**. That is the nearest passing step, not a new colour.

**3. Two order statuses are the same colour.** Badge fills, pairwise:

| Pair | ΔE2000 | Verdict (threshold 10) |
|---|---|---|
| Confirmed vs Delivered | **1.75** | **FAIL** |
| Confirmed vs Processing | 8.73 | FAIL (marginal) |
| Confirmed vs Shipped | 13.68 | PASS |
| Processing vs Shipped | 16.92 | PASS |
| Processing vs Delivered | 10.16 | PASS |
| Shipped vs Delivered | 14.68 | PASS |

ΔE 1.75 is below the threshold of human discrimination in adjacent patches. Confirmed and Delivered are, to the eye, one colour. Snapping Delivered's fill darker on the same hue (112°) gives **`#C7E5C3` at ΔE 10.05** from Confirmed.

Confirmed vs Processing at 8.73 is left **unchanged and recorded**: badges always carry their status word (a checklist requirement), so colour is the second channel, not the only one. Raising it would push the Confirmed green away from the brand green for a distinction the text already makes. This is a deliberate accepted failure, and Task 4 records it as such.

Badge *text* contrast passes everywhere (4.87:1 to 8.27:1), so no text colour changes.

---

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `frontend/src/layouts/MobileNav.tsx` | Drawer wrapper around `Sidebar` for below-`lg`; owns the trigger and the open/close state | 1 |
| `frontend/src/layouts/MobileNav.test.tsx` | Drawer opens, closes, is labelled, and is absent at `lg` | 1 |
| `frontend/src/layouts/DashboardShell.tsx` | Compose sidebar / mobile nav / sticky header / grid | 1 |
| `frontend/src/layouts/Header.tsx` | Accept and render the mobile nav trigger slot | 1 |
| `frontend/src/lib/color.ts` | sRGB → Lab, CIEDE2000, WCAG contrast. Pure functions, no React | 2 |
| `frontend/src/lib/color.test.ts` | Verifies the maths against published reference values | 2 |
| `frontend/src/index.css` | The three-layer token set — real values | 3 |
| `frontend/src/lib/chart-theme.ts` | Every series and status colour, with the snapping decisions recorded | 4 |
| `frontend/src/lib/palette.test.ts` | Enforces the thresholds so a bad edit fails the build | 4 |
| `frontend/src/lib/no-raw-hex.test.ts` | Fails when a component contains a literal hex | 5 |

---

## Task 1: Mobile navigation and a sticky header

The sidebar is `hidden lg:block` with nothing behind it, so below 1024px the app has **no navigation at all**. For a mobile-first Filipino audience this is the most severe open defect in the frontend, which is why it leads Phase B rather than trailing the token work.

**Files:**
- Create: `frontend/src/layouts/MobileNav.tsx`, `frontend/src/layouts/MobileNav.test.tsx`
- Modify: `frontend/src/layouts/DashboardShell.tsx`, `frontend/src/layouts/Header.tsx`, `frontend/src/app/locales/en.json`, `frontend/src/app/locales/fil.json`

**Interfaces:**
- Consumes: `Sidebar` from `./Sidebar`, `NAV_ITEMS` from `@/app/nav`
- Produces: `MobileNav` — a component taking no props, rendering its own trigger button

- [ ] **Step 1: Add the two strings to both catalogues**

The catalogues use **flat dotted keys**, not nested objects — match that exactly or `locales.test.ts` will report drift.

In `frontend/src/app/locales/en.json`, after `"header.account"`:

```json
"header.open_menu": "Open navigation menu",
"header.close_menu": "Close navigation menu",
```

In `frontend/src/app/locales/fil.json`, at the same position:

```json
"header.open_menu": "Buksan ang menu ng nabigasyon",
"header.close_menu": "Isara ang menu ng nabigasyon",
```

- [ ] **Step 2: Write the failing test**

Create `frontend/src/layouts/MobileNav.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { MobileNav } from './MobileNav'

function renderNav() {
  return render(
    <MemoryRouter>
      <MobileNav />
    </MemoryRouter>,
  )
}

describe('MobileNav', () => {
  it('exposes a labelled trigger', () => {
    renderNav()
    expect(screen.getByRole('button', { name: /open navigation menu/i })).toBeInTheDocument()
  })

  it('reveals the navigation destinations once opened', async () => {
    const user = userEvent.setup()
    renderNav()

    expect(screen.queryByRole('link', { name: /marketplace/i })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /open navigation menu/i }))

    expect(await screen.findByRole('link', { name: /marketplace/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /payments/i })).toBeInTheDocument()
  })

  it('closes again, so the drawer cannot strand the user', async () => {
    const user = userEvent.setup()
    renderNav()

    await user.click(screen.getByRole('button', { name: /open navigation menu/i }))
    await screen.findByRole('link', { name: /marketplace/i })

    await user.click(screen.getByRole('button', { name: /close navigation menu/i }))

    expect(screen.queryByRole('link', { name: /marketplace/i })).not.toBeInTheDocument()
  })
})
```

The first assertion of the second test matters as much as the last: it proves the destinations are genuinely hidden before opening, so the test cannot pass against a component that simply renders the sidebar inline.

- [ ] **Step 3: Run it and watch it fail**

```bash
cd frontend && npm test -- MobileNav
```

Expected: FAIL — `Failed to resolve import "./MobileNav"`.

- [ ] **Step 4: Implement `MobileNav`**

Create `frontend/src/layouts/MobileNav.tsx`. Base UI 1.7.0 exposes the drawer parts `Root, Trigger, Portal, Backdrop, Popup, Viewport, Content, Title, Description, Close, SwipeArea, Handle` — confirmed present in `node_modules/@base-ui/react/drawer/`. There is no `side` prop; placement is expressed in classes.

```tsx
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Drawer } from '@base-ui/react/drawer'
import { Menu, X } from 'lucide-react'
import { Sidebar } from './Sidebar'

/**
 * The sidebar's below-`lg` equivalent. Without this the app has no navigation
 * on a phone at all, which is the primary reading device for this audience.
 *
 * State is held here rather than left uncontrolled so that a navigation click
 * can close the drawer — an off-canvas menu that survives a route change
 * covers the page the user just asked for.
 */
export function MobileNav() {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)

  return (
    <Drawer.Root open={open} onOpenChange={setOpen}>
      <Drawer.Trigger
        aria-label={t('header.open_menu')}
        className="flex size-11 items-center justify-center rounded-full bg-surface lg:hidden"
      >
        <Menu aria-hidden="true" className="size-5 text-primary" />
      </Drawer.Trigger>

      <Drawer.Portal>
        <Drawer.Backdrop className="fixed inset-0 z-40 bg-black/40" />
        <Drawer.Popup className="fixed inset-y-0 left-0 z-50 w-[260px] max-w-[85vw] bg-surface shadow-xl">
          <Drawer.Title className="sr-only">{t('app.name')}</Drawer.Title>
          <div className="flex justify-end p-2">
            <Drawer.Close
              aria-label={t('header.close_menu')}
              className="flex size-11 items-center justify-center rounded-full"
            >
              <X aria-hidden="true" className="size-5 text-primary" />
            </Drawer.Close>
          </div>
          <div onClick={() => setOpen(false)}>
            <Sidebar />
          </div>
        </Drawer.Popup>
      </Drawer.Portal>
    </Drawer.Root>
  )
}
```

`Sidebar` needs no provider — `useSession()` returns a fixture synchronously in Phase A — so `MemoryRouter` alone is enough to render it in the test.

If a part name fails to resolve, check `node_modules/@base-ui/react/drawer/index.d.ts` for the exact export list before substituting anything — do not swap in a different library. Base UI 1.7.0's drawer has **no `side` or `placement` prop**; left placement is expressed purely in the `Drawer.Popup` classes above, which is why they are `inset-y-0 left-0` rather than a prop.

- [ ] **Step 5: Run the test until it passes**

```bash
cd frontend && npm test -- MobileNav
```

Expected: PASS, 3 tests.

- [ ] **Step 6: Wire it into the header and make the header sticky**

In `frontend/src/layouts/Header.tsx`, add the import:

```tsx
import { MobileNav } from './MobileNav'
```

Replace the opening `<header>` tag:

```tsx
<header className="sticky top-0 z-30 flex items-center gap-4 bg-page px-6 py-4">
```

`bg-page` is required, not decorative — a transparent sticky header lets content scroll visibly underneath it.

Then make `MobileNav` the first child of the header, before the search field:

```tsx
<MobileNav />
```

- [ ] **Step 7: Verify the whole suite**

```bash
cd frontend && npm test && npm run build
```

Expected: PASS, **19 tests** across 7 files; build clean.

- [ ] **Step 8: Commit**

```bash
git add frontend/src frontend/src/app/locales
git commit -m "feat(frontend): add mobile navigation drawer and sticky header"
```

---

## Task 2: Colour maths

The perceptual check has to live in the codebase, not in a transcript. These are pure functions with no React and no Tailwind, so they are trivially testable and reusable by Task 4's contract test.

**Files:**
- Create: `frontend/src/lib/color.ts`, `frontend/src/lib/color.test.ts`

**Interfaces:**
- Consumes: nothing
- Produces: `contrastRatio(a: string, b: string) => number`, `deltaE2000(a: string, b: string) => number` — both taking `#RRGGBB` strings

- [ ] **Step 1: Write the failing test**

Create `frontend/src/lib/color.test.ts`. The expected values are published reference points, not values copied from our own implementation — a test that asserts what the code happens to do proves nothing.

```ts
import { describe, expect, it } from 'vitest'
import { contrastRatio, deltaE2000 } from './color'

describe('contrastRatio', () => {
  it('returns 21:1 for black on white', () => {
    expect(contrastRatio('#000000', '#FFFFFF')).toBeCloseTo(21, 1)
  })

  it('returns 1:1 for a colour against itself', () => {
    expect(contrastRatio('#3A942A', '#3A942A')).toBeCloseTo(1, 5)
  })

  it('is symmetric — order of arguments must not matter', () => {
    expect(contrastRatio('#FCAE11', '#FFFFFF')).toBeCloseTo(
      contrastRatio('#FFFFFF', '#FCAE11'),
      5,
    )
  })
})

describe('deltaE2000', () => {
  it('is zero for identical colours', () => {
    expect(deltaE2000('#3A942A', '#3A942A')).toBeCloseTo(0, 5)
  })

  it('matches the published CIEDE2000 value for white vs black', () => {
    // L*=100 vs L*=0 reduces to dL/SL with SL = 1 + 0.015*(50-50)^2/... = 1
    expect(deltaE2000('#FFFFFF', '#000000')).toBeCloseTo(100, 0)
  })

  it('separates the mockup greens by more than the 20-unit threshold', () => {
    expect(deltaE2000('#3A942A', '#09410E')).toBeGreaterThan(20)
  })
})
```

- [ ] **Step 2: Run to verify failure**

```bash
cd frontend && npm test -- color
```

Expected: FAIL — `Failed to resolve import "./color"`.

- [ ] **Step 3: Implement**

Create `frontend/src/lib/color.ts`:

```ts
/**
 * Colour maths for the palette contract test.
 *
 * Deliberately dependency-free: this runs in CI on every commit, and a
 * colour library is a large surface to take on for two functions.
 */

type Rgb = readonly [number, number, number]

function parseHex(hex: string): Rgb {
  const value = hex.replace('#', '')
  if (!/^[0-9a-fA-F]{6}$/.test(value)) {
    throw new Error(`Expected a #RRGGBB colour, received "${hex}"`)
  }
  return [
    parseInt(value.slice(0, 2), 16),
    parseInt(value.slice(2, 4), 16),
    parseInt(value.slice(4, 6), 16),
  ] as const
}

/** sRGB channel (0-255) to linear-light. */
function linearise(channel: number): number {
  const c = channel / 255
  return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
}

function relativeLuminance(rgb: Rgb): number {
  const [r, g, b] = rgb.map(linearise)
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

/** WCAG 2.2 contrast ratio. 4.5 for small text, 3.0 for graphics and large text. */
export function contrastRatio(a: string, b: string): number {
  const la = relativeLuminance(parseHex(a))
  const lb = relativeLuminance(parseHex(b))
  const [lighter, darker] = la > lb ? [la, lb] : [lb, la]
  return (lighter + 0.05) / (darker + 0.05)
}

function toLab(rgb: Rgb): readonly [number, number, number] {
  const [r, g, b] = rgb.map(linearise)
  const x = r * 0.4124564 + g * 0.3575761 + b * 0.1804375
  const y = r * 0.2126729 + g * 0.7151522 + b * 0.072175
  const z = r * 0.0193339 + g * 0.119192 + b * 0.9503041

  // D65 reference white.
  const f = (t: number) => (t > 216 / 24389 ? Math.cbrt(t) : (841 / 108) * t + 4 / 29)
  const fx = f(x / 0.95047)
  const fy = f(y / 1.0)
  const fz = f(z / 1.08883)

  return [116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)] as const
}

const rad = (deg: number) => (deg * Math.PI) / 180
const deg = (r: number) => (r * 180) / Math.PI

/**
 * CIEDE2000 colour difference. Roughly: <1 imperceptible, ~2-3 noticeable
 * side by side, >10 clearly different colours.
 */
export function deltaE2000(a: string, b: string): number {
  const [L1, a1, b1] = toLab(parseHex(a))
  const [L2, a2, b2] = toLab(parseHex(b))

  const C1 = Math.hypot(a1, b1)
  const C2 = Math.hypot(a2, b2)
  const Cbar = (C1 + C2) / 2
  const G = 0.5 * (1 - Math.sqrt(Cbar ** 7 / (Cbar ** 7 + 25 ** 7)))

  const a1p = (1 + G) * a1
  const a2p = (1 + G) * a2
  const C1p = Math.hypot(a1p, b1)
  const C2p = Math.hypot(a2p, b2)

  const h1p = C1p === 0 ? 0 : (deg(Math.atan2(b1, a1p)) + 360) % 360
  const h2p = C2p === 0 ? 0 : (deg(Math.atan2(b2, a2p)) + 360) % 360

  const dLp = L2 - L1
  const dCp = C2p - C1p
  const dhp = C1p * C2p === 0 ? 0 : ((h2p - h1p + 180) % 360) - 180
  const dHp = 2 * Math.sqrt(C1p * C2p) * Math.sin(rad(dhp) / 2)

  const Lbp = (L1 + L2) / 2
  const Cbp = (C1p + C2p) / 2

  let hbp: number
  if (C1p * C2p === 0) hbp = h1p + h2p
  else if (Math.abs(h1p - h2p) <= 180) hbp = (h1p + h2p) / 2
  else if (h1p + h2p < 360) hbp = (h1p + h2p + 360) / 2
  else hbp = (h1p + h2p - 360) / 2

  const T =
    1 -
    0.17 * Math.cos(rad(hbp - 30)) +
    0.24 * Math.cos(rad(2 * hbp)) +
    0.32 * Math.cos(rad(3 * hbp + 6)) -
    0.2 * Math.cos(rad(4 * hbp - 63))

  const Sl = 1 + (0.015 * (Lbp - 50) ** 2) / Math.sqrt(20 + (Lbp - 50) ** 2)
  const Sc = 1 + 0.045 * Cbp
  const Sh = 1 + 0.015 * Cbp * T
  const Rt =
    -2 *
    Math.sqrt(Cbp ** 7 / (Cbp ** 7 + 25 ** 7)) *
    Math.sin(2 * rad(30 * Math.exp(-(((hbp - 275) / 25) ** 2))))

  return Math.sqrt(
    (dLp / Sl) ** 2 + (dCp / Sc) ** 2 + (dHp / Sh) ** 2 + Rt * (dCp / Sc) * (dHp / Sh),
  )
}
```

- [ ] **Step 4: Run to verify pass**

```bash
cd frontend && npm test -- color
```

Expected: PASS, 6 tests. Suite total **25**.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/lib/color.ts frontend/src/lib/color.test.ts
git commit -m "feat(frontend): add CIEDE2000 and WCAG contrast helpers"
```

---

## Task 3: Real token values

Only values change here. Every token **name** in `index.css` stays exactly as it is, which is the whole point of having written components against names in Phase A — this task should move no component code.

**Files:**
- Modify: `frontend/src/index.css`

**Interfaces:**
- Consumes: the measured hexes table above
- Produces: no new names; `--color-*` values become real

- [ ] **Step 1: Replace the primitive and semantic blocks**

In `frontend/src/index.css`, replace the `@theme` block's Primitive and Semantic sections (leave the shadcn contract block and `--radius-lg` untouched):

```css
  /* --- Primitive -------------------------------------------------------
   * Measured from `plan/AniKo assets/aniko logo.png` and the mockup PNG with
   * Pillow. These are the brand's actual colours, not an interpretation.
   */
  --color-green-950: #023c16;  /* mockup chrome: sidebar active, CTA, Buy toggle */
  --color-green-900: #004824;  /* logo wordmark — 55.7% of the mark */
  --color-green-700: #09410e;  /* chart: Vegetables (Mixed) */
  --color-green-600: #3a942a;  /* chart: Rice (White) */
  --color-green-500: #54a818;  /* logo light leaf */
  --color-gold-500: #f0a800;   /* logo wheat and arrow */
  --color-amber-600: #c88703;  /* chart: Corn, snapped for contrast — see chart-theme.ts */
  --color-cream-50: #fbfaf8;   /* page background */
  --color-cream-100: #fcfaf7;  /* sidebar background */
  --color-slate-500: #64748b;
  --color-slate-200: #e2e8f0;
  --color-red-600: #f4481a;    /* mockup: negative delta */

  /* --- Semantic --------------------------------------------------------- */
  --color-primary: var(--color-green-950);
  --color-primary-strong: var(--color-green-900);
  --color-primary-foreground: #ffffff;
  --color-accent: var(--color-green-500);
  --color-accent-foreground: #ffffff;
  --color-highlight: var(--color-gold-500);
  --color-page: var(--color-cream-50);
  --color-surface: #fdfdfd;
  --color-muted-fg: var(--color-slate-500);
```

Note `--color-primary` now points at the mockup's chrome green rather than the logo's wordmark green. The logo is printed on white at large sizes; the UI green sits behind small text and needs the extra depth. Both are brand greens — this picks the one the designer actually used for chrome.

- [ ] **Step 2: Delete the Phase A placeholder warning**

Remove this from the comment block at the top of the file, since it is no longer true:

```
 * PHASE A NOTE: every value below is a placeholder. Phase B derives the real
 * palette from `plan/AniKo assets/aniko logo.png` and runs the perceptual
 * distance checks. Components reference the NAMES, so Phase B changes values
 * here and nothing else moves.
```

Replace it with:

```
 * Values are measured from the brand assets — see
 * `docs/superpowers/plans/2026-08-16-aniko-dashboard-phase-b.md` for the
 * sampling method and the perceptual-check results. Components reference the
 * NAMES; changing a value here must not require touching a component.
```

- [ ] **Step 3: Verify nothing moved**

```bash
cd frontend && npm test && npm run build
```

Expected: PASS, **25 tests**, build clean. If a component test fails here, that component was reaching past the token layer and the failure is the point — fix the component, not the token.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/index.css
git commit -m "feat(frontend): replace placeholder tokens with measured brand values"
```

---

## Task 4: Chart theme and the palette contract

Chart colour lives in exactly one file, and the decisions behind each value live next to it. The contract test is what stops this file drifting back to pretty-but-illegible.

**Files:**
- Create: `frontend/src/lib/chart-theme.ts`, `frontend/src/lib/palette.test.ts`

**Interfaces:**
- Consumes: `deltaE2000`, `contrastRatio` from `./color`
- Produces: `SERIES` (`rice | corn | vegetables` → hex), `STATUS` (`confirmed | processing | shipped | delivered` → `{ fill, text }`), `DELTA` (`up | down` → hex), `PLOT_BACKGROUND`

- [ ] **Step 1: Write the failing contract test**

Create `frontend/src/lib/palette.test.ts`:

```ts
import { describe, expect, it } from 'vitest'
import { contrastRatio, deltaE2000 } from './color'
import { DELTA, PLOT_BACKGROUND, SERIES, STATUS } from './chart-theme'

const SERIES_MIN_DELTA_E = 20
const GRAPHIC_MIN_CONTRAST = 3.0
const TEXT_MIN_CONTRAST = 4.5
const STATUS_MIN_DELTA_E = 10

/** Confirmed vs Processing is a known, deliberate exception — see chart-theme.ts. */
const ACCEPTED_STATUS_COLLISIONS = new Set(['confirmed|processing'])

describe('chart series', () => {
  const entries = Object.entries(SERIES)

  it.each(entries)('%s is legible against the plot background', (_name, hex) => {
    expect(contrastRatio(hex, PLOT_BACKGROUND)).toBeGreaterThanOrEqual(GRAPHIC_MIN_CONTRAST)
  })

  it('keeps every pair of series perceptually distinct', () => {
    for (let i = 0; i < entries.length; i += 1) {
      for (let j = i + 1; j < entries.length; j += 1) {
        const [aName, a] = entries[i]
        const [bName, b] = entries[j]
        expect(
          deltaE2000(a, b),
          `${aName} (${a}) vs ${bName} (${b}) are too close to tell apart`,
        ).toBeGreaterThanOrEqual(SERIES_MIN_DELTA_E)
      }
    }
  })
})

describe('status badges', () => {
  const entries = Object.entries(STATUS)

  it.each(entries)('%s text is readable on its own fill', (_name, tone) => {
    expect(contrastRatio(tone.text, tone.fill)).toBeGreaterThanOrEqual(TEXT_MIN_CONTRAST)
  })

  it('keeps fills distinguishable, except where explicitly accepted', () => {
    for (let i = 0; i < entries.length; i += 1) {
      for (let j = i + 1; j < entries.length; j += 1) {
        const [aName, a] = entries[i]
        const [bName, b] = entries[j]
        if (ACCEPTED_STATUS_COLLISIONS.has(`${aName}|${bName}`)) continue
        expect(
          deltaE2000(a.fill, b.fill),
          `${aName} and ${bName} fills are the same colour to the eye`,
        ).toBeGreaterThanOrEqual(STATUS_MIN_DELTA_E)
      }
    }
  })
})

describe('delta indicators', () => {
  it.each(Object.entries(DELTA))('%s is legible on the card surface', (_name, hex) => {
    expect(contrastRatio(hex, '#FDFDFD')).toBeGreaterThanOrEqual(TEXT_MIN_CONTRAST)
  })
})
```

- [ ] **Step 2: Run to verify failure**

```bash
cd frontend && npm test -- palette
```

Expected: FAIL — `Failed to resolve import "./chart-theme"`.

- [ ] **Step 3: Write `chart-theme.ts`**

Create `frontend/src/lib/chart-theme.ts`:

```ts
/**
 * The single home for every series, status and delta colour.
 *
 * A component that reaches for a hex is a review finding — `no-raw-hex.test.ts`
 * enforces this. Values here are measured from the mockup, then snapped where
 * they failed a check. Both runs are recorded so the next person does not
 * re-litigate a decision that has already been measured.
 *
 * ── Run 1: as drawn in the mockup ────────────────────────────────────────
 *   Series separation (CIEDE2000, threshold 20):
 *     Rice #3A942A vs Corn #FCAE11 .......... 41.90  PASS
 *     Rice #3A942A vs Vegetables #09410E .... 28.09  PASS
 *     Corn #FCAE11 vs Vegetables #09410E .... 63.71  PASS
 *   The spec predicted this would fail — "two greens and an amber". It does
 *   not. The designer separated the greens by lightness. Hues are left alone.
 *
 *   Series legibility on white (WCAG 1.4.11, threshold 3.0:1):
 *     Vegetables #09410E .................... 11.84  PASS
 *     Rice       #3A942A ....................  3.85  PASS
 *     Corn       #FCAE11 ....................  1.87  FAIL
 *
 *   Status fill separation (CIEDE2000, threshold 10):
 *     Confirmed vs Delivered ................  1.75  FAIL
 *     Confirmed vs Processing ...............  8.73  FAIL (accepted, see below)
 *     Confirmed vs Shipped .................. 13.68  PASS
 *     Processing vs Shipped ................. 16.92  PASS
 *     Processing vs Delivered ............... 10.16  PASS
 *     Shipped vs Delivered .................. 14.68  PASS
 *
 * ── Run 2: after snapping ────────────────────────────────────────────────
 *   Corn      #FCAE11 -> #C88703   1.87:1 -> 3.03:1
 *     Hue held at 40 degrees and saturation at 0.98; only lightness moved.
 *     The nearest passing step on the same scale, not a new colour.
 *   Delivered #E5F3E3 -> #C7E5C3   dE 1.75 -> 10.05 vs Confirmed
 *     Hue held at 112 degrees. Delivered is the terminal success state, so
 *     reading as the deeper green is right rather than merely convenient.
 *
 * ── Accepted failure ─────────────────────────────────────────────────────
 *   Confirmed vs Processing stays at dE 8.73, below the threshold of 10.
 *   Every badge renders its status word, so colour is the second channel and
 *   not the only one. Pushing Confirmed further would drag it off the brand
 *   green to make a distinction the text already makes. `palette.test.ts`
 *   lists this pair explicitly so the exemption is visible, not silent.
 */

/** The chart plots on the card surface, which is near-white. */
export const PLOT_BACKGROUND = '#FDFDFD'

export const SERIES = {
  rice: '#3A942A',
  corn: '#C88703',
  vegetables: '#09410E',
} as const

export const STATUS = {
  confirmed: { fill: '#E9F3E5', text: '#2C4D38' },
  processing: { fill: '#FDF3DF', text: '#876439' },
  shipped: { fill: '#E3EFFE', text: '#305593' },
  delivered: { fill: '#C7E5C3', text: '#2F573C' },
} as const

export const DELTA = {
  up: '#2D753A',
  down: '#F4481A',
} as const

export type SeriesKey = keyof typeof SERIES
export type StatusKey = keyof typeof STATUS
export type DeltaKey = keyof typeof DELTA
```

- [ ] **Step 4: Run the contract test**

```bash
cd frontend && npm test -- palette
```

Expected: PASS, 12 tests. If `delta down` fails on contrast, that is a genuine finding about `#F4481A` on white — snap its lightness the same way Corn was snapped and record the run in the header comment rather than lowering the threshold.

- [ ] **Step 5: Run everything**

```bash
cd frontend && npm test && npm run build
```

Expected: PASS, **37 tests**, build clean.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/lib/chart-theme.ts frontend/src/lib/palette.test.ts
git commit -m "feat(frontend): add chart theme with recorded perceptual checks"
```

---

## Task 5: Forbid raw hex in components

The "no raw hex" rule is worth nothing as a convention. As a test it survives the first hurried afternoon.

**Files:**
- Create: `frontend/src/lib/no-raw-hex.test.ts`

**Interfaces:**
- Consumes: nothing
- Produces: nothing — this is a guard

- [ ] **Step 1: Write the test**

Create `frontend/src/lib/no-raw-hex.test.ts`:

```ts
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

const SRC = join(process.cwd(), 'src')

/** Colour is allowed to be literal only in the files whose job is defining it. */
const ALLOWED = new Set(['chart-theme.ts', 'color.ts', 'color.test.ts', 'palette.test.ts'])

function walk(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const full = join(dir, entry)
    if (statSync(full).isDirectory()) return walk(full)
    return /\.tsx?$/.test(entry) ? [full] : []
  })
}

describe('colour discipline', () => {
  it('keeps literal hex out of components', () => {
    const offenders = walk(SRC)
      .filter((file) => !ALLOWED.has(file.split(/[\\/]/).pop() ?? ''))
      .flatMap((file) => {
        const hits = readFileSync(file, 'utf8').match(/#[0-9a-fA-F]{6}\b/g) ?? []
        return hits.map((hit) => `${file.replace(SRC, 'src')}: ${hit}`)
      })

    expect(offenders, 'use a token or chart-theme.ts instead of a literal colour').toEqual([])
  })
})
```

- [ ] **Step 2: Run it**

```bash
cd frontend && npm test -- no-raw-hex
```

Expected: PASS. If it fails, the listed file genuinely contains a hex — replace it with a token rather than adding the file to `ALLOWED`. `ALLOWED` is for files that *define* colour, not files that got caught.

- [ ] **Step 3: Run everything and commit**

```bash
cd frontend && npm test && npm run build
git add frontend/src/lib/no-raw-hex.test.ts
git commit -m "test(frontend): fail the build when a component hardcodes a colour"
```

Expected: PASS, **38 tests**, build clean.

---

## Definition of done

- Navigation is reachable at 360px: a labelled trigger opens a drawer, the drawer closes, and a destination click dismisses it
- The header stays put when the page scrolls, over an opaque background
- Every value in `index.css` traces to a measured hex from the logo or the mockup
- `chart-theme.ts` is the only place a series, status or delta colour is written down
- `palette.test.ts` fails if any threshold is breached; the one accepted exception is named in code
- `no-raw-hex.test.ts` fails if a component hardcodes a colour
- `npm test` green at **38 tests**, `npm run build` clean
- Phase B fully ticked in `plan/dashboard plan/CHECKLIST.md`, with the Phase A carry-over note removed

## Explicitly not in this plan

- `StatTile` and any other component — Phase C
- Recharts, the chart wrapper, and rendering the trends panel — Phase D. This phase produces the *colours*, not the chart
- Applying `STATUS` to a rendered badge — Phase G, when `RecentOrdersPanel` exists
- Dark mode. The semantic layer makes it possible later; nothing here assumes it
- The 360px low-end Android walkthrough and the Filipino locale pass — human walkthroughs, tracked at the bottom of the checklist
