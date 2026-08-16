import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { formatDistance } from '@/lib/format'
import type { NearbySupplier } from './types'

/**
 * First letter of the first two words. No photo assets exist for these
 * suppliers and inventing them would mean either a placeholder that 404s or a
 * stock image that misrepresents a real farm, so the thumbnail is initials —
 * which also costs nothing to load on a rural connection.
 */
function initials(name: string): string {
  return name
    .split(/\s+/)
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? '')
    .join('')
}

export function SupplierRow({ supplier }: { supplier: NearbySupplier }) {
  const { t, i18n } = useTranslation()

  return (
    <li className="flex items-start gap-3 py-3">
      <Avatar size="lg" className="mt-0.5">
        <AvatarFallback className="font-semibold text-primary">
          {initials(supplier.name)}
        </AvatarFallback>
      </Avatar>

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="truncate font-semibold text-primary">{supplier.name}</p>
          {/*
            The panel already filters to verified suppliers, so this badge is
            not a filter indicator — it is the reason a buyer can trust the row
            at all, and the spec calls for it to be visible per supplier.

            Deliberately `primary`, not `accent`. The theme defines
            `--color-accent-foreground` as white, but white on the leaf green
            measures 3.00:1 — enough for a graphic, short of the 4.5:1 that
            badge-sized text needs. On primary it is 12.65:1.
          */}
          <Badge>{t('suppliers.verified')}</Badge>
        </div>

        <p className="truncate text-sm text-muted-fg">{supplier.region}</p>

        {/*
          The distance is rendered from the value the hook computed and never
          recomputed here. Two code paths to the same number is exactly how a
          row and its pin come to disagree.
        */}
        <p className="mt-0.5 text-sm text-muted-fg">
          {t('suppliers.distance_away', {
            distance: formatDistance(supplier.distanceKm, i18n.language),
          })}
        </p>

        {/*
          Spans rather than a nested <ul>: the row itself is the list item, and
          a list inside a list item makes every crop tag a `listitem` too —
          which drowns the rows in any query that counts them, for no gain the
          badge text does not already carry.
        */}
        <div className="mt-2 flex flex-wrap items-center gap-1.5">
          {supplier.crops.map((crop) => (
            <Badge key={crop} variant="outline" className="text-muted-fg">
              {t(`crop.${crop}`)}
            </Badge>
          ))}

          {/*
            No `/suppliers/:id` route exists yet, so this deep-links into the
            marketplace instead of pointing at a dead `#`. The id travels as a
            query param because that is what Phase I will read when the real
            profile route lands — the link changes target then, not shape.

            The visible text is the same three words on every row, so the
            accessible name carries the supplier name. Otherwise a screen-reader
            user listing the links hears "View Profile" six times over.
          */}
          <Link
            to={`/marketplace?supplier=${encodeURIComponent(supplier.id)}`}
            aria-label={t('suppliers.view_profile_for', { name: supplier.name })}
            className="ml-auto rounded-lg px-2 py-1 text-sm font-medium text-primary underline underline-offset-2"
          >
            {t('suppliers.view_profile')}
          </Link>
        </div>
      </div>
    </li>
  )
}
