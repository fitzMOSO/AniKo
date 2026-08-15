import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Drawer } from '@base-ui/react/drawer'
import { Menu, X } from 'lucide-react'
import { Sidebar } from './Sidebar'

/**
 * The sidebar's below-`lg` equivalent. Without this the app has no navigation
 * on a phone at all, which is the primary reading device for this audience.
 *
 * State is held here rather than left uncontrolled so that a navigation click
 * can close the drawer — an off-canvas menu that survives a route change
 * covers the page the user just asked for.
 */
export function MobileNav() {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)

  return (
    // swipeDirection defaults to 'down' (a bottom sheet). This panel is anchored
    // left, so the dismiss gesture has to point left too or the two disagree.
    <Drawer.Root open={open} onOpenChange={setOpen} swipeDirection="left">
      <Drawer.Trigger
        aria-label={t('header.open_menu')}
        className="flex size-11 shrink-0 items-center justify-center rounded-full bg-surface lg:hidden"
      >
        <Menu aria-hidden="true" className="size-5 text-primary" />
      </Drawer.Trigger>

      <Drawer.Portal>
        <Drawer.Backdrop className="fixed inset-0 z-40 bg-black/40" />
        <Drawer.Popup className="fixed inset-y-0 left-0 z-50 flex w-[260px] max-w-[85vw] flex-col bg-surface shadow-xl">
          <Drawer.Title className="sr-only">{t('app.name')}</Drawer.Title>
          <div className="flex justify-end p-2">
            <Drawer.Close
              aria-label={t('header.close_menu')}
              className="flex size-11 items-center justify-center rounded-full"
            >
              <X aria-hidden="true" className="size-5 text-primary" />
            </Drawer.Close>
          </div>
          {/*
            Click delegation rather than a prop on Sidebar: Sidebar is shared
            with the desktop layout, where it has no drawer to close. React
            fires click for keyboard link activation too, so this is not
            mouse-only.
          */}
          <div className="min-h-0 flex-1" onClick={() => setOpen(false)}>
            <Sidebar />
          </div>
        </Drawer.Popup>
      </Drawer.Portal>
    </Drawer.Root>
  )
}
