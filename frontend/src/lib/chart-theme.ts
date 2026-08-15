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
 *   The spec predicted this would fail — "two greens and an amber is exactly
 *   the arrangement that fails a perceptual-distance check". It does not. The
 *   designer separated the greens by lightness. The hues are left alone.
 *
 *   Series legibility on the plot background (WCAG 1.4.11, threshold 3.0:1):
 *     Vegetables #09410E .................... 11.64  PASS
 *     Rice       #3A942A ....................  3.79  PASS
 *     Corn       #FCAE11 ....................  1.85  FAIL
 *
 *   Status fill separation (CIEDE2000, threshold 10):
 *     Confirmed vs Delivered ................  1.75  FAIL
 *     Confirmed vs Processing ...............  8.73  FAIL (accepted, see below)
 *     Confirmed vs Shipped .................. 13.68  PASS
 *     Processing vs Shipped ................. 16.92  PASS
 *     Processing vs Delivered ............... 10.16  PASS
 *     Shipped vs Delivered .................. 14.68  PASS
 *
 *   Delta legibility on the plot background (threshold 4.5:1, these are text):
 *     up   #2D753A ..........................  5.55  PASS
 *     down #F4481A ..........................  3.57  FAIL
 *
 * ── Run 2: after snapping ────────────────────────────────────────────────
 *   Every snap holds hue and saturation and moves lightness only, so each is
 *   the nearest passing step on the same scale rather than a new colour.
 *
 *   Corn      #FCAE11 -> #C68502   1.85:1 -> 3.06:1   (hue 40.1 held)
 *     Separation survives the move: dE 37.20 vs Rice, 47.46 vs Vegetables.
 *   Delivered #E5F3E3 -> #C7E5C3   dE 1.75 -> 10.05 vs Confirmed (hue 112 held)
 *     Delivered is the terminal success state, so reading as the deeper green
 *     is right rather than merely convenient.
 *   Delta down #F4481A -> #DA360A  3.57:1 -> 4.57:1   (hue 12.7 held)
 *
 *   NOTE: the targets are measured against PLOT_BACKGROUND (#FDFDFD), not pure
 *   white. Snapping against #FFFFFF lands Corn at 3.03:1 on white but 2.98:1
 *   on the surface it is actually drawn on — a failure disguised as a pass.
 *
 * ── Accepted failure ─────────────────────────────────────────────────────
 *   Confirmed vs Processing stays at dE 8.73, below the threshold of 10.
 *   Every badge renders its status word, so colour is the second channel and
 *   not the only one. Pushing Confirmed further would drag it off the brand
 *   green to make a distinction the text already makes. `palette.test.ts`
 *   lists this pair explicitly, so the exemption is visible rather than
 *   silently absorbed into a lowered threshold.
 */

/** The chart plots on the card surface, which is near-white but not white. */
export const PLOT_BACKGROUND = '#FDFDFD'

export const SERIES = {
  rice: '#3A942A',
  corn: '#C68502',
  vegetables: '#09410E',
} as const

/**
 * The mockup washes a pale green under the Rice line only. It is decoration,
 * not data — it encodes no value a reader could misread — so it is deliberately
 * far too light to be taken for a fourth series, and no contrast threshold
 * applies to it.
 */
export const SERIES_FILL = {
  rice: '#EAF5E6',
} as const

export const STATUS = {
  confirmed: { fill: '#E9F3E5', text: '#2C4D38' },
  processing: { fill: '#FDF3DF', text: '#876439' },
  shipped: { fill: '#E3EFFE', text: '#305593' },
  delivered: { fill: '#C7E5C3', text: '#2F573C' },
} as const

export const DELTA = {
  up: '#2D753A',
  down: '#DA360A',
} as const

export type SeriesKey = keyof typeof SERIES
export type StatusKey = keyof typeof STATUS
export type DeltaKey = keyof typeof DELTA
