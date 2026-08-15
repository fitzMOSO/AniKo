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
      date: '2025-08-23',
      rice: 48,
      corn: 32,
      vegetables: 17,
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
