import { useTranslation } from 'react-i18next'
import { LotsScroller } from './LotsScroller'
import { useFeaturedLots } from './useFeaturedLots'

/**
 * Panel chrome only — heading plus the scroller, matching
 * `MarketPriceTrendsPanel` and `NearbySuppliersPanel`. It holds no state, so
 * there is exactly one place that decides which lots exist
 * (`useFeaturedLots`) and one place that decides how they are laid out
 * (`LotsScroller`).
 *
 * Nothing here is lazy-loaded. The whole feature is plain DOM and Base UI,
 * which is already in the entry chunk for the buttons and badges; a dynamic
 * import would add a round trip on a rural connection to defer nothing.
 */
export function FeaturedLotsPanel() {
  const { t } = useTranslation()
  const { lots } = useFeaturedLots()

  return (
    <div className="rounded-xl bg-surface p-5">
      <h2 className="text-lg font-bold text-primary">{t('lots.title')}</h2>

      <div className="mt-4">
        <LotsScroller lots={lots} />
      </div>
    </div>
  )
}
