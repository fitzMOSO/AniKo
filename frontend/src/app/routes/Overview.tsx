import { useTranslation } from 'react-i18next'
import { StatTilesRow } from '@/features/overview/StatTilesRow'
import { useSession } from '@/lib/session'

/**
 * Empty slots on purpose. Phases D-G fill the rest:
 *   pricing   -> MarketPriceTrends     (Phase D)
 *   suppliers -> NearbySuppliers       (Phase E)
 *   lots      -> FeaturedLots          (Phase F)
 *   orders    -> RecentOrders          (Phase G)
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
      <section data-slot="pricing" className="col-span-full lg:col-span-8" />
      <section data-slot="suppliers" className="col-span-full lg:col-span-4" />
      <section data-slot="lots" className="col-span-full lg:col-span-8" />
      <section data-slot="orders" className="col-span-full lg:col-span-4" />
    </>
  )
}
