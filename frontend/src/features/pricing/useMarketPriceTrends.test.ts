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

  // The range selector is real, not decorative — the spec is explicit about
  // this. If every range returned the same slice, the control would look
  // functional and do nothing, which is worse than omitting it.
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
