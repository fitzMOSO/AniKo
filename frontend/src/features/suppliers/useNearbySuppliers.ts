import { useMemo } from 'react'
import { haversineKm } from '@/lib/geo'
import { ALL_SUPPLIERS, BUYER_LOCATION } from './fixtures'
import type { NearbySuppliersResult } from './types'

/**
 * Phase I swaps this body for `GET /api/v1/suppliers/nearby?lat=&lng=&radius_km=`.
 * Until then the filtering and the distance maths happen here, which is also
 * where the "pin and row cannot disagree" guarantee lives: both the map and the
 * list consume this one result, so there is no second path to the same data.
 */
export function useNearbySuppliers(): NearbySuppliersResult {
  const suppliers = useMemo(
    () =>
      ALL_SUPPLIERS.filter((supplier) => supplier.verified)
        .map((supplier) => ({
          ...supplier,
          distanceKm: haversineKm(BUYER_LOCATION, supplier.location),
        }))
        .sort((a, b) => a.distanceKm - b.distanceKm),
    [],
  )

  return { suppliers, origin: BUYER_LOCATION, isLoading: false }
}
