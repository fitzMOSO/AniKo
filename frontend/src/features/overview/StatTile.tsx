import { useTranslation } from 'react-i18next'
import { ArrowDownRight, ArrowUpRight } from 'lucide-react'
import { DELTA } from '@/lib/chart-theme'
import { formatCount, formatCurrency, formatPercent } from '@/lib/format'
import type { OverviewStat } from './types'

/**
 * One dashboard stat. Not interactive: the mockup gives these no affordance,
 * and wrapping a figure in a button invents a destination that does not exist.
 */
export function StatTile({ stat }: { stat: OverviewStat }) {
  const { t, i18n } = useTranslation()
  const locale = i18n.language

  const value =
    stat.format === 'currency'
      ? formatCurrency(stat.value, locale)
      : formatCount(stat.value, locale)

  const percent = formatPercent(stat.deltaPercent, locale)
  const flat = stat.deltaPercent === 0
  const rose = stat.deltaPercent > 0

  // Colour follows the meaning, not the sign. A fall in Pending Orders is good
  // news for a buyer, and a rise in Spend is not a success — so `upIsGood` is
  // consulted here, and the sign is used only to pick the arrow below.
  const isGoodNews = rose ? stat.upIsGood : !stat.upIsGood
  const colour = flat ? undefined : isGoodNews ? DELTA.up : DELTA.down

  const Arrow = rose ? ArrowUpRight : ArrowDownRight

  const deltaLabel = flat
    ? t('stats.delta_flat')
    : t(rose ? 'stats.delta_up' : 'stats.delta_down', { percent })

  return (
    <article className="rounded-xl bg-surface p-5">
      <div className="flex items-start gap-4">
        <span
          aria-hidden="true"
          className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-page"
        >
          <stat.icon className="size-6 text-primary" />
        </span>

        <div className="min-w-0">
          <p className="text-sm font-medium text-muted-fg">{t(stat.labelKey)}</p>
          <p data-testid="value" className="mt-1 text-3xl font-bold text-primary">
            {value}
          </p>
        </div>
      </div>

      <p className="mt-4 flex items-center gap-1 text-xs">
        {/*
          One accessible label on the whole delta rather than per-fragment: a
          screen reader announcing "up", "12%" and "vs last month" as three
          separate nodes is worse than one sentence.
        */}
        <span
          data-testid="delta"
          aria-label={deltaLabel}
          style={{ color: colour }}
          className="flex items-center gap-0.5 font-semibold"
        >
          {!flat && <Arrow aria-hidden="true" className="size-3.5" />}
          {percent}
        </span>
        <span aria-hidden="true" className="text-muted-fg">
          {t('stats.vs_last_month')}
        </span>
      </p>
    </article>
  )
}
