import { lazy, Suspense } from 'react'
import type { LatLng } from '@/lib/geo'
import { SupplierMapSkeleton } from './SupplierMapSkeleton'
import type { NearbySupplier } from './types'

/*
 * The second split point in the app, and the one with a hard number behind it.
 *
 * Measured: the entry chunk is 392.67 kB raw against Vite's 500 kB warning
 * threshold, and Leaflet adds ~155 kB raw. It does not tree-shake — its
 * package.json declares no `module`, no `exports` and no `sideEffects`, so
 * importing `MapContainer` alone costs 43.80 kB gzip against 44.99 kB for
 * importing the entire library. 392.67 + 155 breaches the threshold, so the
 * boundary is not a preference.
 *
 * It sits around the MAP rather than around the panel, which is the important
 * part: the heading and the supplier list render from the entry chunk, and stay
 * fully usable on the connections where the tiles may never arrive at all. The
 * split is what makes the list-first promise true rather than aspirational.
 */
const Map = lazy(() => import('./SupplierMap').then((m) => ({ default: m.SupplierMap })))

export function LazySupplierMap({
  suppliers,
  origin,
}: {
  suppliers: NearbySupplier[]
  origin: LatLng
}) {
  return (
    <Suspense fallback={<SupplierMapSkeleton />}>
      <Map suppliers={suppliers} origin={origin} />
    </Suspense>
  )
}
