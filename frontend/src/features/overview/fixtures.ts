import { Bookmark, MessageSquare, ShoppingCart, Wallet } from 'lucide-react'
import type { OverviewStat } from './types'

/**
 * The Buyer set, per the spec's Phase C. The mockup shows the Farmer set
 * (Active Listings / This Month Sales) even though its own session chip reads
 * "Buyer" and its Buy toggle is active — that is a mockup inconsistency, and
 * the Farmer set is Phase H.
 *
 * Magnitudes are borrowed from the mockup so the layout is exercised at
 * realistic widths. The peso figure is scaled to a plausible PH value rather
 * than carrying the mockup's US dollars across at face value.
 */
export const BUYER_STATS: OverviewStat[] = [
  {
    key: 'new_inquiries',
    labelKey: 'stats.new_inquiries',
    icon: MessageSquare,
    value: 16,
    format: 'count',
    deltaPercent: 6,
    upIsGood: true,
  },
  {
    key: 'pending_orders',
    labelKey: 'stats.pending_orders',
    icon: ShoppingCart,
    value: 9,
    format: 'count',
    deltaPercent: -2,
    upIsGood: false,
  },
  {
    key: 'saved_lots',
    labelKey: 'stats.saved_lots',
    icon: Bookmark,
    value: 28,
    format: 'count',
    deltaPercent: 12,
    upIsGood: true,
  },
  {
    key: 'spend_this_month',
    labelKey: 'stats.spend_this_month',
    icon: Wallet,
    value: 2_671_400,
    format: 'currency',
    deltaPercent: 18,
    upIsGood: false,
  },
]
