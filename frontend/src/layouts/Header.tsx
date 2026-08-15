import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Search, SlidersHorizontal, Bell, User, Leaf } from 'lucide-react'
import { cn } from '@/lib/utils'
import { MobileNav } from './MobileNav'

type Mode = 'buy' | 'sell'

export function Header() {
  const { t } = useTranslation()
  const [mode, setMode] = useState<Mode>('buy')

  const modeButton = (value: Mode, labelKey: string) => (
    <button
      type="button"
      aria-pressed={mode === value}
      onClick={() => setMode(value)}
      className={cn(
        'min-h-11 rounded-full px-6 text-sm font-semibold',
        mode === value ? 'bg-primary text-white' : 'text-primary',
      )}
    >
      {t(labelKey)}
    </button>
  )

  return (
    // `bg-page` is load-bearing, not decorative: a transparent sticky header
    // lets the page scroll visibly underneath it.
    <header className="sticky top-0 z-30 flex items-center gap-4 bg-page px-6 py-4">
      <MobileNav />

      <div className="relative flex-1">
        <Search
          aria-hidden="true"
          className="absolute left-4 top-1/2 size-5 -translate-y-1/2 text-muted-fg"
        />
        <input
          type="search"
          aria-label={t('header.search_placeholder')}
          placeholder={t('header.search_placeholder')}
          className="min-h-11 w-full rounded-full bg-surface py-3 pl-12 pr-12 text-sm"
        />
        <button
          type="button"
          aria-label={t('header.filters')}
          className="absolute right-2 top-1/2 flex size-11 -translate-y-1/2 items-center justify-center"
        >
          <SlidersHorizontal aria-hidden="true" className="size-5 text-primary" />
        </button>
      </div>

      <div className="flex items-center rounded-full bg-surface p-1">
        {modeButton('buy', 'header.mode_buy')}
        {modeButton('sell', 'header.mode_sell')}
      </div>

      <button
        type="button"
        className="flex min-h-11 items-center gap-2 rounded-full bg-primary px-6 text-sm font-semibold text-white"
      >
        <Leaf aria-hidden="true" className="size-4" />
        {t('header.list_produce')}
      </button>

      <button
        type="button"
        aria-label={t('header.notifications')}
        className="flex size-11 items-center justify-center rounded-full bg-surface"
      >
        <Bell aria-hidden="true" className="size-5 text-primary" />
      </button>

      <button
        type="button"
        aria-label={t('header.account')}
        className="flex size-11 items-center justify-center rounded-full bg-surface"
      >
        <User aria-hidden="true" className="size-5 text-primary" />
      </button>
    </header>
  )
}
