import type { PricePoint, RangeMonths } from './types'

/**
 * Twelve months of weekly prices, generated rather than hand-typed — 156
 * numbers by hand is a transcription-error factory.
 *
 * The walk is a sum of two sines, NOT `Math.random`. A random fixture gives a
 * plot that changes on every run, which makes a visual regression impossible to
 * spot and an exact assertion impossible to write. This is deterministic, so
 * `fixtures.test.ts` pins the first point by value.
 *
 * The bands are chosen so the three crops never cross. In the mockup that
 * separation is what lets a reader follow three series at once; two crossing
 * lines would undo the perceptual-distance work recorded in chart-theme.ts.
 */
const WEEKS = 52

/**
 * The Saturday the most recent week closes on. A constant, not `new Date()`: a
 * fixture that drifts with the system clock fails on a future date for reasons
 * that have nothing to do with the code under test.
 */
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

/**
 * Exact week counts rather than `months * 4.345` rounded. A range selector that
 * returns 13 points on one code path and 14 on another is a bug report waiting
 * to be filed.
 */
export const WEEKS_PER_RANGE: Record<RangeMonths, number> = { 3: 13, 6: 26, 12: 52 }
