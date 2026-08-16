import { renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { FEATURED_LOTS } from './fixtures'
import { useFeaturedLots } from './useFeaturedLots'

describe('useFeaturedLots', () => {
  it('returns the featured set in the order it was curated in', () => {
    const { result } = renderHook(() => useFeaturedLots())
    expect(result.current.lots.map((lot) => lot.id)).toEqual(FEATURED_LOTS.map((lot) => lot.id))
  })

  it('is not loading, because the fixture is already here', () => {
    const { result } = renderHook(() => useFeaturedLots())
    expect(result.current.isLoading).toBe(false)
  })

  /*
   * The cards keep their own bookmark state. If the array identity changed on
   * every render, React would remount the cards and quietly reset it, which
   * looks exactly like a bookmark that does not stick.
   */
  it('keeps a stable array identity across renders', () => {
    const { result, rerender } = renderHook(() => useFeaturedLots())
    const first = result.current.lots
    rerender()
    expect(result.current.lots).toBe(first)
  })

  /*
   * A minimum order larger than the volume on offer is unsellable — the card
   * would show a buyer a lot they cannot legally buy any quantity of.
   */
  it('never asks for a minimum order the lot cannot supply', () => {
    const { result } = renderHook(() => useFeaturedLots())
    for (const lot of result.current.lots) {
      expect(lot.minOrderKg, lot.id).toBeGreaterThan(0)
      expect(lot.minOrderKg, lot.id).toBeLessThanOrEqual(lot.volumeKg)
    }
  })

  /*
   * The Verified overlay is only meaningful if it can be absent. A fixture set
   * where every lot is verified makes the badge untestable and, worse, makes
   * it read as decoration to anyone reviewing the UI.
   */
  it('features both verified and unverified lots, so the badge means something', () => {
    const { result } = renderHook(() => useFeaturedLots())
    expect(result.current.lots.some((lot) => lot.verified)).toBe(true)
    expect(result.current.lots.some((lot) => !lot.verified)).toBe(true)
  })
})
