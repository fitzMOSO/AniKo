import { Bookmark } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { formatCurrency } from '@/lib/format'
import { RequestQuoteDialog } from './RequestQuoteDialog'
import type { Lot } from './types'
import { formatVolume } from './volume'

/**
 * First letter of the first two words of the lot name, for the photo stand-in.
 */
function initials(name: string): string {
  return name
    .split(/\s+/)
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? '')
    .join('')
}

export function LotCard({ lot }: { lot: Lot }) {
  const { t, i18n } = useTranslation()

  /*
   * Bookmark state lives here, not in the panel.
   *
   * There is no persistence this phase, and the "Saved Lots" stat tile already
   * has its own fixture number. Hoisting a session-only Set into the panel
   * would create a second, silently disagreeing source of truth for that count.
   * When Phase I gives bookmarks an endpoint the state moves up to whatever
   * owns the mutation — one place, once.
   */
  const [bookmarked, setBookmarked] = useState(false)

  return (
    <li className="flex w-72 shrink-0 flex-col rounded-xl border border-border bg-surface p-3">
      {/*
        No photo assets exist for these lots. The precedent set by SupplierRow
        applies unchanged: a referenced image that 404s is worse than no image,
        and a stock photo of somebody else's farm misrepresents the goods a
        buyer is about to request a quote on. So the frame is a token-surfaced
        block carrying the lot's initials — it reserves the exact space the real
        photo will take in Phase I, costs nothing on a rural connection, and
        claims nothing untrue.

        `aria-hidden` because it is decoration: every character in it is
        already in the heading directly below.
      */}
      <div className="relative">
        <div
          aria-hidden="true"
          data-testid="lot-photo"
          className="flex h-32 w-full items-center justify-center rounded-lg bg-muted text-2xl font-bold text-muted-fg"
        >
          {initials(lot.name)}
        </div>

        {/*
          The overlay is absolutely positioned over the photo frame, per the
          checklist, and only rendered when the lot is actually verified —
          a badge every card carries is decoration, not a signal.

          `default` (primary) rather than `accent`: the theme's
          `--color-accent-foreground` is white, which measures 3.00:1 on the
          leaf green — short of the 4.5:1 badge-sized text needs. Same call as
          SupplierRow.
        */}
        {lot.verified && <Badge className="absolute top-2 left-2">{t('lots.verified')}</Badge>}

        {/*
          Bookmark is a toggle, so its accessible name changes with its state.
          A toggle whose name never changes leaves a screen-reader user unable
          to tell "save this" from "already saved"; `aria-pressed` alone is not
          enough when the icon is the only other cue.
        */}
        <Button
          type="button"
          variant="secondary"
          size="icon"
          aria-pressed={bookmarked}
          aria-label={t(bookmarked ? 'lots.bookmark_remove' : 'lots.bookmark_add', {
            name: lot.name,
          })}
          onClick={() => setBookmarked((on) => !on)}
          className="absolute top-2 right-2"
        >
          <Bookmark aria-hidden="true" fill={bookmarked ? 'currentColor' : 'none'} />
        </Button>
      </div>

      <h3 className="mt-3 truncate font-semibold text-primary">{lot.name}</h3>
      <p className="truncate text-sm text-muted-fg">{lot.supplier}</p>
      <p className="truncate text-sm text-muted-fg">{lot.region}</p>

      {/*
        Spans, not a nested list: this card is already a list item, and a list
        inside it would make every tag a `listitem` too, drowning any query
        that counts the cards.
      */}
      <div className="mt-2 flex flex-wrap gap-1.5">
        <Badge variant="outline" className="text-muted-fg">
          {t(`crop.${lot.crop}`)}
        </Badge>
        <Badge variant="outline" className="text-muted-fg">
          {t('lots.grade', { grade: lot.grade })}
        </Badge>
      </div>

      {/*
        Price is the number a buyer scans for, so it gets the weight. It goes
        through `formatCurrency` — the symbol is the locale's business, and a
        typed peso sign here would be the one place that never follows a
        currency change.
      */}
      <p className="mt-3 text-lg font-bold text-primary">
        {t('lots.price_per_kg', { price: formatCurrency(lot.pricePerKg, i18n.language) })}
      </p>
      <p className="text-sm text-muted-fg">
        {t('lots.volume', { volume: formatVolume(lot.volumeKg, i18n.language) })}
      </p>
      <p className="text-sm text-muted-fg">
        {t('lots.min_order', { volume: formatVolume(lot.minOrderKg, i18n.language) })}
      </p>

      {/* `mt-auto` so the CTA lines up across cards of unequal text length. */}
      <div className="mt-auto flex pt-4">
        <RequestQuoteDialog lot={lot} />
      </div>
    </li>
  )
}
