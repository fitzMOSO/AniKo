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
