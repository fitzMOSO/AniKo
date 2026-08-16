import type { SeriesKey } from '@/lib/chart-theme'

export interface Lot {
  id: string
  /** What is being sold, e.g. "Premium White Rice". */
  name: string
  /** Reuses the chart's crop keys so a lot and a price series agree. */
  crop: SeriesKey
  /** Trade grade as printed on the sack — a letter, not a score out of five. */
  grade: string
  supplier: string
  /** Municipality and province, pre-composed for display. */
  region: string
  /**
   * Whether the SUPPLIER behind this lot is verified. Unlike the suppliers
   * panel, this panel does not filter on it: the badge is a per-lot claim the
   * buyer reads, so a set where every card carries it would make the badge
   * decoration rather than information.
   */
  verified: boolean
  /** Everything stored in kilogrammes; only the display layer picks a unit. */
  volumeKg: number
  minOrderKg: number
  /** Pesos per kilogramme. The mockup's dollars are US placeholder data. */
  pricePerKg: number
}

export interface FeaturedLotsResult {
  lots: Lot[]
  isLoading: boolean
}
