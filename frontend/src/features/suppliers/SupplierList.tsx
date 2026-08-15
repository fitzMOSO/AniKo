import { useTranslation } from 'react-i18next'
import { SupplierRow } from './SupplierRow'
import type { NearbySupplier } from './types'

/**
 * The list is the panel's primary interface — the spec requires it to stay
 * usable on connections where the map tiles may never load, so it takes its
 * suppliers as a prop and knows nothing about Leaflet.
 *
 * It also does not sort. `useNearbySuppliers` owns the ordering; a second sort
 * here would be a second opinion about which supplier is nearest.
 */
export function SupplierList({ suppliers }: { suppliers: NearbySupplier[] }) {
  const { t } = useTranslation()

  if (suppliers.length === 0) {
    return <p className="py-6 text-sm text-muted-fg">{t('suppliers.empty')}</p>
  }

  return (
    <ul aria-label={t('suppliers.list_label')} className="divide-y divide-border">
      {suppliers.map((supplier) => (
        <SupplierRow key={supplier.id} supplier={supplier} />
      ))}
    </ul>
  )
}
