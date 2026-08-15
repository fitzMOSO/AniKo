import { useMemo } from 'react'
import { WEEKLY_PRICES, WEEKS_PER_RANGE } from './fixtures'
import type { MarketPriceTrendsResult, RangeMonths } from './types'

/**
 * Phase I swaps this body for `GET /api/v1/pricing/trends?months=`. The
 * signature is the contract and does not change when that happens — which is
 * why the panel never reaches for the fixture directly.
 */
export function useMarketPriceTrends(months: RangeMonths): MarketPriceTrendsResult {
  const points = useMemo(() => WEEKLY_PRICES.slice(-WEEKS_PER_RANGE[months]), [months])
  return { points, isLoading: false }
}
