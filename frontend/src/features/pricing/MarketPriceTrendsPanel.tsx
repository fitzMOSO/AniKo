import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Area, CartesianGrid, ComposedChart, Line, Tooltip, XAxis, YAxis } from 'recharts'
import { ChartContainer, type ChartConfig } from '@/components/ui/chart'
import { SERIES, SERIES_FILL } from '@/lib/chart-theme'
import { formatCurrency } from '@/lib/format'
import { useMarketPriceTrends } from './useMarketPriceTrends'
import { RANGE_MONTHS, type RangeMonths } from './types'

const CROPS = ['rice', 'corn', 'vegetables'] as const

export function MarketPriceTrendsPanel() {
  const { t, i18n } = useTranslation()
  const [months, setMonths] = useState<RangeMonths>(6)
  const { points } = useMarketPriceTrends(months)

  /*
   * Colour enters the chart ONLY through this config. `ChartContainer` turns it
   * into `--color-rice` / `--color-corn` / `--color-vegetables` CSS variables,
   * so the series below reference `var(--color-*)` and never a literal. That is
   * what keeps `no-raw-hex.test.ts` green while the hexes stay in chart-theme.
   */
  const config = {
    rice: { label: t('pricing.rice'), color: SERIES.rice },
    corn: { label: t('pricing.corn'), color: SERIES.corn },
    vegetables: { label: t('pricing.vegetables'), color: SERIES.vegetables },
  } satisfies ChartConfig

  // Month only, not the raw ISO date: 52 full dates on one axis is noise.
  const tickLabel = (iso: string) =>
    new Date(`${iso}T00:00:00Z`).toLocaleDateString(i18n.language, {
      month: 'short',
      timeZone: 'UTC',
    })

  return (
    <div className="rounded-xl bg-surface p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-bold text-primary">{t('pricing.title')}</h2>

        <label className="sr-only" htmlFor="pricing-range">
          {t('pricing.range_label')}
        </label>
        {/*
          A native select rather than a styled dropdown. The mockup draws a
          custom chevron control, but this is keyboard- and screen-reader-correct
          for free, and it survives a range change without a portal.
        */}
        <select
          id="pricing-range"
          value={months}
          onChange={(e) => setMonths(Number(e.target.value) as RangeMonths)}
          className="rounded-lg border border-border bg-surface px-3 py-1.5 text-sm text-primary"
        >
          {RANGE_MONTHS.map((m) => (
            <option key={m} value={m}>
              {t(`pricing.range_${m}`)}
            </option>
          ))}
        </select>
      </div>

      {/*
        The legend is real DOM above the plot, not Recharts' <Legend>, which
        renders inside the SVG where it is neither above the plot nor reliably
        readable by assistive technology.
      */}
      <ul data-testid="legend" className="mt-4 flex flex-wrap gap-x-6 gap-y-2">
        {CROPS.map((crop) => (
          <li key={crop} className="flex items-center gap-2 text-sm text-primary">
            <span
              aria-hidden="true"
              className="h-0.5 w-4 rounded-full"
              style={{ backgroundColor: SERIES[crop] }}
            />
            {config[crop].label}
          </li>
        ))}
      </ul>

      <p className="mt-4 text-xs text-muted-fg">{t('pricing.axis_label')}</p>

      <ChartContainer
        config={config}
        role="img"
        aria-label={t('pricing.chart_label')}
        className="mt-1 h-[280px] w-full"
      >
        <ComposedChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
          <CartesianGrid vertical={false} stroke="var(--color-border)" />
          <XAxis
            dataKey="date"
            tickFormatter={tickLabel}
            tickLine={false}
            axisLine={false}
            minTickGap={24}
            tick={{ fill: 'var(--color-muted-fg)', fontSize: 12 }}
          />
          <YAxis
            tickLine={false}
            axisLine={false}
            width={44}
            tick={{ fill: 'var(--color-muted-fg)', fontSize: 12 }}
          />
          <Tooltip
            formatter={(value, name) => [
              formatCurrency(Number(value), i18n.language),
              config[name as (typeof CROPS)[number]]?.label ?? name,
            ]}
            labelFormatter={tickLabel}
          />
          {/* Decoration only — see SERIES_FILL in chart-theme.ts. */}
          <Area dataKey="rice" stroke="none" fill={SERIES_FILL.rice} isAnimationActive={false} />
          {CROPS.map((crop) => (
            <Line
              key={crop}
              dataKey={crop}
              stroke={`var(--color-${crop})`}
              strokeWidth={2}
              dot={{ r: 2.5 }}
              isAnimationActive={false}
            />
          ))}
        </ComposedChart>
      </ChartContainer>

      <p className="mt-3 text-center text-xs text-muted-fg">{t('pricing.source')}</p>
    </div>
  )
}
