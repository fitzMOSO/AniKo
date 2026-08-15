import { NavLink } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { BadgeCheck, HelpCircle } from 'lucide-react'
import { NAV_ITEMS } from '@/app/nav'
import { useSession } from '@/lib/session'
import { cn } from '@/lib/utils'

export function Sidebar() {
  const { t } = useTranslation()
  const { user } = useSession()

  return (
    <aside className="flex h-full w-[220px] flex-col bg-surface px-4 py-6">
      <div className="px-2">
        <img src="/brand/aniko-logo.png" alt={t('app.name')} className="h-8 w-auto" />
        <p className="mt-1 text-xs text-muted-fg">{t('app.tagline')}</p>
      </div>

      <nav className="mt-8 flex flex-1 flex-col gap-1">
        {NAV_ITEMS.map((item) => (
          <NavLink
            key={item.key}
            to={item.to}
            className={({ isActive }) =>
              cn(
                'flex min-h-11 items-center gap-3 rounded-lg px-3 text-sm font-medium',
                isActive
                  ? 'bg-sidebar-item-active-bg text-sidebar-item-active-fg'
                  : 'text-primary hover:bg-page',
              )
            }
          >
            <item.icon aria-hidden="true" className="size-5 shrink-0" />
            <span className="flex-1">{t(item.labelKey)}</span>
            {item.badge ? (
              <span
                aria-label={t('nav.unread_messages', { count: item.badge })}
                className="rounded-full bg-accent px-2 py-0.5 text-xs font-semibold text-white"
              >
                {item.badge}
              </span>
            ) : null}
          </NavLink>
        ))}
      </nav>

      {user ? (
        <div className="rounded-xl border border-page p-3">
          <p className="text-sm font-semibold text-primary">{user.name}</p>
          <p className="text-xs text-muted-fg">
            {t(user.role === 'buyer' ? 'session.role_buyer' : 'session.role_farmer')}
          </p>
          {user.verified ? (
            <p className="mt-3 flex items-center gap-2 text-xs font-medium text-primary">
              <BadgeCheck aria-hidden="true" className="size-4 text-accent" />
              {t('session.verified_account')}
            </p>
          ) : null}
        </div>
      ) : null}

      <a
        href="/help"
        className="mt-4 flex min-h-11 items-center gap-2 px-3 text-sm text-muted-fg"
      >
        <HelpCircle aria-hidden="true" className="size-4" />
        {t('nav.help')}
      </a>
    </aside>
  )
}
