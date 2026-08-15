import type { LatLng } from '@/lib/geo'
import type { SeriesKey } from '@/lib/chart-theme'

export interface Supplier {
  id: string
  name: string
  /** Municipality and province, pre-composed for display. */
  region: string
  /** The single source of truth for where this supplier is. */
  location: LatLng
  verified: boolean
  /** Reuses the chart's crop keys so a supplier and a price series agree. */
  crops: SeriesKey[]
}

/** A supplier with its distance derived from `location`, never stored. */
export interface NearbySupplier extends Supplier {
  distanceKm: number
}

export interface NearbySuppliersResult {
  suppliers: NearbySupplier[]
  /** Where distances are measured from — also where the map centres. */
  origin: LatLng
  isLoading: boolean
}
