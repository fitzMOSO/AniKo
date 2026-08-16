import { useMemo } from 'react'
import { FEATURED_LOTS } from './fixtures'
import type { FeaturedLotsResult } from './types'

/**
 * Phase I swaps this body for `GET /api/v1/lots/featured`.
 *
 * It deliberately does NOT sort or filter. "Featured" is a merchandising
 * decision made upstream — by a merchandiser today, by the endpoint tomorrow —
 * and any ordering invented here would be a second opinion that the real
 * response then silently contradicts. The suppliers hook sorts because
 * distance is something it can actually compute; nothing here is.
 *
 * The array identity is stable across renders so the scroller's children do
 * not remount and lose their bookmark state on every parent update.
 */
export function useFeaturedLots(): FeaturedLotsResult {
  const lots = useMemo(() => FEATURED_LOTS, [])

  return { lots, isLoading: false }
}
