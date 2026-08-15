import type { LucideIcon } from 'lucide-react'

/**
 * One stat tile's data. Deliberately raw: `value` and `deltaPercent` are
 * numbers, not strings, so the view layer can localise them. Phase I replaces
 * the adapter behind `useOverviewStats` and must preserve this shape.
 */
export interface OverviewStat {
  key: string
  labelKey: string
  icon: LucideIcon
  value: number
  format: 'count' | 'currency'
  /** Signed. The sign drives the arrow; `upIsGood` drives the colour. */
  deltaPercent: number
  /**
   * Whether a rise is good news. Read instead of the sign, because falling
   * Pending Orders is good for a buyer and rising Spend is not a success.
   */
  upIsGood: boolean
}
