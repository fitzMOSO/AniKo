import { useTranslation } from 'react-i18next'
import { LotCard } from './LotCard'
import type { Lot } from './types'

/**
 * The horizontal scroller.
 *
 * Two things it gets right that this pattern usually gets wrong:
 *
 * 1. KEYBOARD REACH. A scrollable box is not focusable by default, so a
 *    keyboard user can tab to the buttons inside the cards but can never scroll
 *    the container to reach the cards further along — the content simply does
 *    not exist for them. `tabIndex={0}` makes the region focusable so the arrow
 *    keys act on it, and because a bare focusable div announces nothing it also
 *    carries `role="region"` and an accessible name. The name is required: an
 *    unnamed region is dropped from the landmark list entirely.
 *
 * 2. TOUCH SCROLL. The container scrolls on ONE axis only. There is no
 *    `touch-action` clamp and no `touchmove` handler, so a vertical swipe that
 *    starts on a card is still the page's to handle — the classic failure is a
 *    carousel that swallows every touch and strands the reader mid-page.
 *    `overscroll-x-contain` stops a horizontal fling from chaining outward once
 *    the strip hits its end, which is the only chaining worth suppressing.
 *
 * Like SupplierList, it neither sorts nor filters: `useFeaturedLots` owns the
 * order.
 */
export function LotsScroller({ lots }: { lots: Lot[] }) {
  const { t } = useTranslation()

  if (lots.length === 0) {
    return <p className="py-6 text-sm text-muted-fg">{t('lots.empty')}</p>
  }

  return (
    <div
      role="region"
      tabIndex={0}
      aria-label={t('lots.scroller_label')}
      className="-mx-1 overflow-x-auto overscroll-x-contain px-1 pb-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
    >
      {/* `items-stretch` so every card is as tall as the tallest, which is what
          keeps the Request Quote buttons on one line. */}
      <ul className="flex items-stretch gap-4">
        {lots.map((lot) => (
          <LotCard key={lot.id} lot={lot} />
        ))}
      </ul>
    </div>
  )
}
