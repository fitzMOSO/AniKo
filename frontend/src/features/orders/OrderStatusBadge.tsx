import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { STATUS, type StatusKey } from '@/lib/chart-theme'

/**
 * The status word is the primary channel; the fill is the second one.
 *
 * That ordering is load-bearing, not a preference. `palette.test.ts` exempts
 * the Confirmed/Processing fill pair from the perceptual-distance threshold on
 * the stated grounds that every badge renders its word — so the exemption is
 * only honest while this component is incapable of rendering a bare swatch.
 * There is no icon-only or colour-only variant, and no prop that would suppress
 * the label; `OrderStatusBadge.test.tsx` asserts the word for all four statuses
 * so the exemption stays backed by a test rather than by good intentions.
 *
 * Colour arrives through `style` from the STATUS map. A Tailwind class cannot
 * express these values without a literal somewhere in this file, which the
 * raw-colour guard rejects — and rightly, since the palette belongs in one
 * place where it can be measured.
 */
export function OrderStatusBadge({ status }: { status: StatusKey }) {
  const { t } = useTranslation()
  const tone = STATUS[status]

  return (
    <Badge
      data-status={status}
      className="px-2.5 py-0.5 font-semibold"
      style={{ backgroundColor: tone.fill, color: tone.text }}
    >
      {t(`orders.status_${status}`)}
    </Badge>
  )
}
