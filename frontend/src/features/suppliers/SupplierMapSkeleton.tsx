import { useTranslation } from 'react-i18next'
import { MAP_HEIGHT_CLASS } from './mapLayout'

/**
 * Suspense fallback for the lazily-loaded map.
 *
 * Same height as the map, from the same constant, so the supplier list below it
 * does not get shoved down the moment the Leaflet chunk arrives. On the rural
 * connections this app targets, that gap is seconds long, and the list is the
 * part the reader is already using.
 */
export function SupplierMapSkeleton() {
  const { t } = useTranslation()

  return (
    <div role="status" aria-live="polite">
      <span className="sr-only">{t('suppliers.map_loading')}</span>
      <div
        aria-hidden="true"
        className={`${MAP_HEIGHT_CLASS} w-full animate-pulse rounded-lg bg-muted`}
      />
    </div>
  )
}
