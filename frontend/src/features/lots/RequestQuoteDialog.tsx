import { Dialog } from '@base-ui/react/dialog'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import type { Lot } from './types'

/**
 * Request Quote, trigger and modal together.
 *
 * It posts nothing. That is the phase's honest position and the copy says so
 * out loud (`lots.quote_body`) rather than showing a spinner and a tick for a
 * request that was never made — a fake success state is worse than no button,
 * because the buyer stops waiting for a reply that is never coming.
 *
 * Base UI's Dialog rather than a hand-rolled overlay or a native `<dialog>`:
 * it is already a dependency, `modal` (the default) traps focus and returns it
 * to the trigger on close, and Escape closes without us binding a key handler
 * that would then have to be tested for stopping at the right boundary.
 */
export function RequestQuoteDialog({ lot }: { lot: Lot }) {
  const { t } = useTranslation()

  return (
    <Dialog.Root>
      {/*
        The visible label is the short "Request Quote" — six cards side by side
        cannot each spell out their lot. The accessible name carries the lot,
        because out of context "Request Quote" names six identical buttons and
        a screen-reader user cannot tell which one they are on.
      */}
      <Dialog.Trigger
        render={<Button size="lg" />}
        className="flex-1"
        aria-label={t('lots.request_quote_for', { name: lot.name })}
      >
        {t('lots.request_quote')}
      </Dialog.Trigger>

      <Dialog.Portal>
        <Dialog.Backdrop className="fixed inset-0 bg-primary/40" />
        <Dialog.Popup className="fixed top-1/2 left-1/2 w-[min(28rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 rounded-xl bg-surface p-5 shadow-lg">
          <Dialog.Title className="text-lg font-bold text-primary">
            {t('lots.quote_title')}
          </Dialog.Title>

          {/*
            Description, not a paragraph we style to look like one: Base UI
            wires it to the popup's `aria-describedby`, so the "not submitted
            yet" caveat is announced with the dialog instead of only being
            found by someone who reads on.
          */}
          <Dialog.Description className="mt-2 text-sm text-muted-fg">
            {t('lots.quote_body', { supplier: lot.supplier })}
          </Dialog.Description>

          <div className="mt-5 flex justify-end">
            <Dialog.Close render={<Button variant="outline" size="lg" />}>
              {t('lots.quote_close')}
            </Dialog.Close>
          </div>
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
