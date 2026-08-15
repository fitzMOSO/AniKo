import { useTranslation } from 'react-i18next'

/**
 * Suspense fallback for the lazily-loaded price chart.
 *
 * The heights below are not decorative. They mirror the real panel box for box
 * — header row, legend, axis caption, the h-[280px] plot, source line, p-5
 * padding — so the grid area is already the right size when the chart chunk
 * lands. A zero-height fallback would let the rest of Overview settle and then
 * shove it down, which is exactly the layout shift this app cannot afford: the
 * spec targets rural connections where this state is on screen for seconds,
 * not frames.
 */
export function MarketPriceTrendsSkeleton() {
  const { t } = useTranslation()

  return (
    <div role="status" aria-live="polite" className="rounded-xl bg-surface p-5">
      <span className="sr-only">{t('pricing.loading')}</span>

      <div aria-hidden="true" className="animate-pulse">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="h-8 w-52 rounded-lg bg-muted" />
          <div className="h-8 w-36 rounded-lg bg-muted" />
        </div>

        <div className="mt-4 flex flex-wrap gap-x-6 gap-y-2">
          <div className="h-5 w-24 rounded-full bg-muted" />
          <div className="h-5 w-24 rounded-full bg-muted" />
          <div className="h-5 w-28 rounded-full bg-muted" />
        </div>

        <div className="mt-4 h-4 w-28 rounded bg-muted" />

        <div className="mt-1 h-[280px] w-full rounded-lg bg-muted" />

        <div className="mx-auto mt-3 h-4 w-44 rounded bg-muted" />
      </div>
    </div>
  )
}
