import { lazy, Suspense } from 'react'
import { MarketPriceTrendsSkeleton } from './MarketPriceTrendsSkeleton'

/*
 * The one split point in the app.
 *
 * Recharts plus `@/components/ui/chart` is roughly half the production bundle,
 * and `MarketPriceTrendsPanel` is the only importer of either. Splitting by
 * route would move nothing — `/overview` is the only real route — so the
 * boundary has to sit inside Overview instead. Behind this `lazy()`, the stat
 * tiles paint from the entry chunk while the charting library streams in
 * separately.
 *
 * The panel keeps a plain named export so its own tests, and anything that
 * wants it eagerly, can import it directly without a Suspense boundary.
 */
const Panel = lazy(() =>
  import('./MarketPriceTrendsPanel').then((m) => ({ default: m.MarketPriceTrendsPanel })),
)

export function LazyMarketPriceTrendsPanel() {
  return (
    <Suspense fallback={<MarketPriceTrendsSkeleton />}>
      <Panel />
    </Suspense>
  )
}
