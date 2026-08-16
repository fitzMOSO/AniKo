import { useTranslation } from 'react-i18next'
import { FeaturedLotsPanel } from '@/features/lots/FeaturedLotsPanel'
import { RecentOrdersPanel } from '@/features/orders/RecentOrdersPanel'
import { StatTilesRow } from '@/features/overview/StatTilesRow'
import { LazyMarketPriceTrendsPanel } from '@/features/pricing/MarketPriceTrendsPanel.lazy'
import { NearbySuppliersPanel } from '@/features/suppliers/NearbySuppliersPanel'
import { useSession } from '@/lib/session'

/**
 * Every slot is filled as of Phase G. Each panel owns its own data — it calls
 * its own hook rather than being handed props from here — so this route stays a
 * layout and nothing else, and Phase I can swap a panel's fixtures for an API
 * call without this file changing at all.
 *
 * Only the pricing panel is lazy. It carries Recharts, which is 368 kB on its
 * own; the rest are small enough that a dynamic import would cost a round trip
 * on a rural connection to defer a few kB.
 */
export function Overview() {
  const { t } = useTranslation()
  const { user } = useSession()

  return (
    <>
      <header className="col-span-full">
        <h1 className="text-3xl font-bold text-primary">
          {t('overview.greeting', { name: user?.name ?? '' })}
        </h1>
        <p className="mt-1 text-sm text-muted-fg">{t('overview.subtitle')}</p>
      </header>

      <section data-slot="stats" className="col-span-full">
        <StatTilesRow />
      </section>
      <section data-slot="pricing" className="col-span-full lg:col-span-8">
        <LazyMarketPriceTrendsPanel />
      </section>
      <section data-slot="suppliers" className="col-span-full lg:col-span-4">
        <NearbySuppliersPanel />
      </section>
      <section data-slot="lots" className="col-span-full lg:col-span-8">
        <FeaturedLotsPanel />
      </section>
      <section data-slot="orders" className="col-span-full lg:col-span-4">
        <RecentOrdersPanel />
      </section>
    </>
  )
}
