import { useMemo } from 'react'
import { RECENT_ORDERS } from './fixtures'
import type { RecentOrdersResult } from './types'

/**
 * Phase I swaps this body for `GET /api/v1/orders/recent?limit=`.
 *
 * The limit is applied here rather than in the panel because it is a data
 * question, not a layout one — when this becomes a request, `limit` is a query
 * parameter and the component must not be re-sliced on top of a server that
 * already sliced. Asking for more than exists returns everything and is not an
 * error: an account with three orders is a new account, not a broken one.
 *
 * The fixture order is preserved. Sorting by estimated delivery would quietly
 * turn "recent orders" into "next arrivals" — a different panel with the same
 * heading, which is worse than no sort at all.
 */
export function useRecentOrders(limit: number): RecentOrdersResult {
  const orders = useMemo(() => RECENT_ORDERS.slice(0, Math.max(0, limit)), [limit])

  return { orders, isLoading: false }
}
