import { useTranslation } from 'react-i18next'
import { useSession } from '@/lib/session'

/**
 * Empty slots on purpose. Phases C-G fill them:
 *   stats     -> StatTilesRow          (Phase C)
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

      <section data-slot="stats" className="col-span-full" />
      <section data-slot="pricing" className="col-span-full lg:col-span-8" />
      <section data-slot="suppliers" className="col-span-full lg:col-span-4" />
      <section data-slot="lots" className="col-span-full lg:col-span-8" />
      <section data-slot="orders" className="col-span-full lg:col-span-4" />
    </>
  )
}
