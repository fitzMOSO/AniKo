import type { StatusKey } from '@/lib/chart-theme'

/**
 * `status` is `StatusKey`, the key type of the STATUS map in chart-theme, on
 * purpose: an order status that has no badge colour, or a badge colour with no
 * order status, becomes a type error rather than a blank badge at runtime. The
 * palette test reasons about exactly those four keys, so the two cannot drift.
 */
export interface Order {
  /** Human-facing reference, shown verbatim — never renumbered for display. */
  id: string
  product: string
  supplier: string
  /** Kilograms. Stored as a number so the view owns the locale formatting. */
  quantityKg: number
  status: StatusKey
  /** ISO `YYYY-MM-DD`. No time part: nobody promises an hour of arrival. */
  estimatedDelivery: string
}

export interface RecentOrdersResult {
  orders: Order[]
  isLoading: boolean
}
