import { useId } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { formatDate, formatWeight } from '@/lib/format'
import { OrderStatusBadge } from './OrderStatusBadge'
import { useRecentOrders } from './useRecentOrders'

/**
 * First letter of the first two words of the product name. There are no
 * product photos in this repo, and the two ways to pretend otherwise are both
 * worse than admitting it: a placeholder path 404s in the reader's face, and a
 * stock crop photo misrepresents the actual lot someone has paid for. Initials
 * are honest, cost nothing on a rural connection, and match the precedent
 * `SupplierRow` already set for supplier avatars.
 */
function initials(name: string): string {
  return name
    .split(/\s+/)
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? '')
    .join('')
}

const CELL = 'px-3 py-3 align-middle text-sm'

export interface RecentOrdersPanelProps {
  /**
   * How many orders the panel asks for. Default 5 — the panel sits in a narrow
   * column beside the wider lots section, and a table taller than its neighbour
   * turns the dashboard into a scroll.
   */
  limit?: number
}

export function RecentOrdersPanel({ limit = 5 }: RecentOrdersPanelProps) {
  const { t, i18n } = useTranslation()
  const { orders } = useRecentOrders(limit)
  const headingId = useId()

  return (
    <div className="rounded-xl bg-surface p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id={headingId} className="text-lg font-bold text-primary">
          {t('orders.title')}
        </h2>

        {/*
          A real router link, not a button and not `href="#"`. `/orders` is in
          `nav.ts` and is routed today (to the Placeholder while Phase H is
          unbuilt), so this navigates, opens in a new tab on middle-click, and
          shows a real target in the status bar. A dead anchor would be a
          promise the dashboard cannot keep.

          Styled `primary` and permanently underlined, not the leaf green the
          mockup uses for links: `SupplierRow` records that the accent green
          measures 3.00:1 against this surface, which is short of the 4.5:1
          that text this size needs. The underline means the link is not
          identified by colour alone either.
        */}
        <Link
          to="/orders"
          className="rounded-lg text-sm font-semibold text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          {t('orders.view_all')}
        </Link>
      </div>

      {orders.length === 0 ? (
        <p className="mt-4 text-sm text-muted-fg">{t('orders.empty')}</p>
      ) : (
        /*
          A real <table>, not a grid of divs. This is genuinely tabular — six
          fields per order, the same six every row — and a screen-reader user
          moving column-wise depends on the header association that only a
          table with `<th scope="col">` provides. The wrapper scrolls
          horizontally on a phone rather than letting the columns collapse into
          each other; dropping columns at narrow widths would hide the delivery
          estimate from exactly the readers most likely to be on a phone.
        */
        <div className="mt-4 -mx-1 overflow-x-auto">
          <table aria-labelledby={headingId} className="w-full min-w-xl border-collapse">
            <thead>
              <tr className="border-b border-border text-left text-xs font-semibold text-muted-fg">
                <th scope="col" className="px-3 py-2">
                  {t('orders.col_id')}
                </th>
                <th scope="col" className="px-3 py-2">
                  {t('orders.col_product')}
                </th>
                <th scope="col" className="px-3 py-2">
                  {t('orders.col_supplier')}
                </th>
                {/*
                  Quantity is right-aligned because digits compare down a
                  column; its header goes with it, or the header stops looking
                  like it belongs to the numbers underneath.
                */}
                <th scope="col" className="px-3 py-2 text-right">
                  {t('orders.col_quantity')}
                </th>
                <th scope="col" className="px-3 py-2">
                  {t('orders.col_status')}
                </th>
                <th scope="col" className="px-3 py-2">
                  {t('orders.col_delivery')}
                </th>
              </tr>
            </thead>

            <tbody>
              {orders.map((order) => (
                <tr key={order.id} className="border-b border-border last:border-0">
                  {/*
                    The order reference is the row's header cell. It is what a
                    reader would name the row by, and marking it up as such
                    means a screen reader announces "ORD-2418, Quantity,
                    1,500 kg" instead of six unattributed values.
                  */}
                  <th scope="row" className={`${CELL} font-semibold text-primary`}>
                    {order.id}
                  </th>

                  <td className={CELL}>
                    <span className="flex items-center gap-2">
                      <Avatar size="sm" aria-hidden="true">
                        <AvatarFallback className="text-[0.65rem] font-semibold text-primary">
                          {initials(order.product)}
                        </AvatarFallback>
                      </Avatar>
                      <span className="font-medium text-primary">{order.product}</span>
                    </span>
                  </td>

                  <td className={`${CELL} text-muted-fg`}>{order.supplier}</td>

                  <td className={`${CELL} text-right tabular-nums text-primary`}>
                    {formatWeight(order.quantityKg, i18n.language)}
                  </td>

                  <td className={CELL}>
                    <OrderStatusBadge status={order.status} />
                  </td>

                  <td className={`${CELL} whitespace-nowrap text-muted-fg`}>
                    {formatDate(order.estimatedDelivery, i18n.language)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
