import { BUYER_STATS } from './fixtures'
import type { OverviewStat } from './types'

export interface OverviewStatsResult {
  stats: OverviewStat[]
  isLoading: boolean
}

/**
 * Phase C returns a fixture synchronously. Phase I swaps the body for a fetch
 * of `GET /api/v1/buyer/overview/stats` — the return shape must not change,
 * because every consumer is already written against `{ stats, isLoading }`.
 */
export function useOverviewStats(): OverviewStatsResult {
  return { stats: BUYER_STATS, isLoading: false }
}
