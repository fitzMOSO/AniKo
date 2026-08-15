import { renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { haversineKm } from '@/lib/geo'
import { ALL_SUPPLIERS, BUYER_LOCATION } from './fixtures'
import { useNearbySuppliers } from './useNearbySuppliers'

function suppliers() {
  return renderHook(() => useNearbySuppliers()).result.current.suppliers
}

describe('useNearbySuppliers', () => {
  // The panel is titled "Nearby Verified Suppliers". If an unverified supplier
  // reached the list, the heading would be a lie — so the fixture deliberately
  // contains one and this asserts it never appears.
  it('excludes unverified suppliers, as the panel heading promises', () => {
    expect(ALL_SUPPLIERS.some((s) => !s.verified)).toBe(true)
    expect(suppliers().every((s) => s.verified)).toBe(true)
  })

  it('orders by distance, nearest first', () => {
    const distances = suppliers().map((s) => s.distanceKm)
    expect([...distances].sort((a, b) => a - b)).toEqual(distances)
  })

  /*
   * The distance must be derived from the coordinates, not stored beside them.
   * This is the guarantee behind "a pin and its row can never disagree": if the
   * two could drift, a row could read 90 km while its pin sat 300 km away.
   */
  it('derives every distance from the supplier coordinates', () => {
    for (const supplier of suppliers()) {
      expect(supplier.distanceKm).toBeCloseTo(
        haversineKm(BUYER_LOCATION, supplier.location),
        6,
      )
    }
  })

  it('reports the origin the distances were measured from', () => {
    const { result } = renderHook(() => useNearbySuppliers())
    expect(result.current.origin).toEqual(BUYER_LOCATION)
  })

  it('keeps every supplier inside a plausible domestic radius', () => {
    for (const supplier of suppliers()) {
      expect(supplier.distanceKm).toBeLessThan(400)
    }
  })

  it('reports a settled state, since the fixture adapter is synchronous', () => {
    const { result } = renderHook(() => useNearbySuppliers())
    expect(result.current.isLoading).toBe(false)
  })
})
