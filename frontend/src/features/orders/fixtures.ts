import type { Order } from './types'

/**
 * Supplier names are copied by value from `features/suppliers/fixtures.ts`
 * rather than imported from it. Phase I replaces both fixture sets with two
 * unrelated endpoints — `/orders` returns a supplier name string, not a
 * `Supplier` — so an import here would model a relationship the API does not
 * have, and would have to be unpicked. The names match so the dashboard reads
 * as one product; the coupling is deliberately only skin deep.
 *
 * Crops and volumes are Philippine: cavans of palay, sacks of white corn,
 * highland vegetables out of Benguet. Ordered newest-delivery-first is NOT the
 * intent — "recent" is about when the order was placed, so the fixture order is
 * the placement order and the hook preserves it (see useRecentOrders).
 */
export const RECENT_ORDERS: Order[] = [
  {
    id: 'ORD-2418',
    product: 'Dinorado Rice',
    supplier: 'Bataan Rice Growers',
    quantityKg: 2000,
    status: 'processing',
    estimatedDelivery: '2026-08-21',
  },
  {
    id: 'ORD-2417',
    product: 'White Corn',
    supplier: 'Golden Harvest Co.',
    quantityKg: 1500,
    status: 'shipped',
    estimatedDelivery: '2026-08-19',
  },
  {
    id: 'ORD-2415',
    product: 'Baguio Beans',
    supplier: 'Valle Verde Produce',
    quantityKg: 450,
    status: 'confirmed',
    estimatedDelivery: '2026-08-24',
  },
  {
    id: 'ORD-2411',
    product: 'Saba Banana',
    supplier: 'Laguna Fresh Collective',
    quantityKg: 900,
    status: 'delivered',
    estimatedDelivery: '2026-08-12',
  },
  {
    id: 'ORD-2408',
    product: 'Yellow Corn',
    supplier: 'GreenFields Farm',
    quantityKg: 3200,
    status: 'delivered',
    estimatedDelivery: '2026-08-08',
  },
]
