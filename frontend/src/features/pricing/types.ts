import type { SeriesKey } from '@/lib/chart-theme'

/**
 * One week's closing price for every crop, in ₱/kg. Shaped for Recharts: one
 * row per x-value with one key per series, which is the layout its `data` prop
 * expects — reshaping in the component would be work done on every render.
 */
export type PricePoint = { date: string } & Record<SeriesKey, number>

export const RANGE_MONTHS = [3, 6, 12] as const
export type RangeMonths = (typeof RANGE_MONTHS)[number]

export interface MarketPriceTrendsResult {
  points: PricePoint[]
  isLoading: boolean
}
