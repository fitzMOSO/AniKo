import { useTranslation } from 'react-i18next'
import { LazySupplierMap } from './SupplierMap.lazy'
import { SupplierList } from './SupplierList'
import { useNearbySuppliers } from './useNearbySuppliers'

/**
 * The panel calls `useNearbySuppliers` exactly once and hands the same array to
 * both the map and the list. That is the whole design: there is no second path
 * to the data, so a pin and its row cannot disagree about which suppliers are
 * verified, how far away they are, or what order they come in.
 *
 * The map is deliberately below the heading and above the list, and is the only
 * lazy part. If its chunk never lands — the rural-connection case the spec
 * names — the reader still gets a titled, ordered, complete list of verified
 * suppliers with distances. The map adds geography to that; it never carries
 * anything the list does not already say.
 */
export function NearbySuppliersPanel() {
  const { t } = useTranslation()
  const { suppliers, origin } = useNearbySuppliers()

  return (
    <div className="rounded-xl bg-surface p-5">
      <h2 className="text-lg font-bold text-primary">{t('suppliers.title')}</h2>

      <div className="mt-4">
        <LazySupplierMap suppliers={suppliers} origin={origin} />
      </div>

      <div className="mt-2">
        <SupplierList suppliers={suppliers} />
      </div>
    </div>
  )
}
