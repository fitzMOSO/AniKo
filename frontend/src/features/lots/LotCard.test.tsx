import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { LotCard } from './LotCard'
import type { Lot } from './types'

function lot(overrides: Partial<Lot> = {}): Lot {
  return {
    id: 'lot-bataan-white-rice',
    name: 'Premium White Rice',
    crop: 'rice',
    grade: 'A',
    supplier: 'Bataan Rice Growers',
    region: 'Balanga, Bataan',
    verified: true,
    volumeKg: 12000,
    minOrderKg: 500,
    pricePerKg: 52,
    ...overrides,
  }
}

/** The card is an <li>, so every test needs a list around it. */
function renderCard(value: Lot = lot()) {
  return render(
    <ul>
      <LotCard lot={value} />
    </ul>,
  )
}

describe('LotCard', () => {
  it('names the lot, its supplier and where it is', () => {
    renderCard()
    expect(screen.getByRole('heading', { name: 'Premium White Rice' })).toBeInTheDocument()
    expect(screen.getByText('Bataan Rice Growers')).toBeInTheDocument()
    expect(screen.getByText('Balanga, Bataan')).toBeInTheDocument()
  })

  it('states the crop and the grade', () => {
    renderCard()
    expect(screen.getByText('Rice')).toBeInTheDocument()
    expect(screen.getByText('Grade A')).toBeInTheDocument()
  })

  /*
   * Pesos, not dollars: the mockup's US figures are placeholder data and the
   * spec overrides them. Coming through `formatCurrency` is the point — a
   * typed peso sign would be the one place a currency change would miss.
   */
  it('prices in pesos per kilogramme', () => {
    renderCard()
    expect(screen.getByText('₱52/kg')).toBeInTheDocument()
  })

  it('states the volume available and the minimum order', () => {
    renderCard()
    expect(screen.getByText('12,000 kg available')).toBeInTheDocument()
    expect(screen.getByText('Min. order 500 kg')).toBeInTheDocument()
  })

  /*
   * No photo assets exist, so the frame is a token-surfaced block with the
   * lot's initials — and it is hidden from assistive technology, because every
   * character in it is already in the heading below.
   */
  it('shows a placeholder frame rather than a broken image', () => {
    renderCard()
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
    const photo = screen.getByTestId('lot-photo')
    expect(photo).toHaveTextContent('PW')
    expect(photo).toHaveAttribute('aria-hidden', 'true')
  })

  it('overlays Verified on a verified lot', () => {
    renderCard()
    expect(screen.getByText('Verified')).toBeInTheDocument()
  })

  it('does not claim Verified for a lot that is not', () => {
    renderCard(lot({ verified: false }))
    expect(screen.queryByText('Verified')).not.toBeInTheDocument()
  })

  describe('bookmark', () => {
    it('starts unsaved, and says so by name', () => {
      renderCard()
      const button = screen.getByRole('button', { name: 'Save Premium White Rice' })
      expect(button).toHaveAttribute('aria-pressed', 'false')
    })

    /*
     * The accessible name has to change with the state. `aria-pressed` alone
     * leaves a screen-reader user unable to tell "save this" from "already
     * saved" when the icon is the only other cue.
     */
    it('renames itself once saved, and toggles back', async () => {
      const user = userEvent.setup()
      renderCard()

      await user.click(screen.getByRole('button', { name: 'Save Premium White Rice' }))
      const pressed = screen.getByRole('button', {
        name: 'Remove Premium White Rice from saved',
      })
      expect(pressed).toHaveAttribute('aria-pressed', 'true')

      await user.click(pressed)
      expect(
        screen.getByRole('button', { name: 'Save Premium White Rice' }),
      ).toHaveAttribute('aria-pressed', 'false')
    })
  })

  describe('request quote', () => {
    /*
     * Six cards side by side would otherwise be six buttons called "Request
     * Quote", indistinguishable out of context.
     */
    it('names the trigger for the lot it belongs to', () => {
      renderCard()
      const trigger = screen.getByRole('button', {
        name: 'Request Quote for Premium White Rice',
      })
      expect(trigger).toHaveTextContent('Request Quote')
    })

    it('opens a modal that says the request has not been sent', async () => {
      const user = userEvent.setup()
      renderCard()

      await user.click(
        screen.getByRole('button', { name: 'Request Quote for Premium White Rice' }),
      )

      const dialog = await screen.findByRole('dialog')
      expect(within(dialog).getByText('Request a Quote')).toBeInTheDocument()
      expect(within(dialog).getByText(/not submitted yet/)).toBeInTheDocument()
      expect(within(dialog).getByText(/Bataan Rice Growers/)).toBeInTheDocument()
    })

    it('closes on Escape', async () => {
      const user = userEvent.setup()
      renderCard()

      await user.click(
        screen.getByRole('button', { name: 'Request Quote for Premium White Rice' }),
      )
      expect(await screen.findByRole('dialog')).toBeInTheDocument()

      await user.keyboard('{Escape}')
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })

    it('closes on its own Close button and hands focus back to the trigger', async () => {
      const user = userEvent.setup()
      renderCard()

      const trigger = screen.getByRole('button', {
        name: 'Request Quote for Premium White Rice',
      })
      await user.click(trigger)

      const dialog = await screen.findByRole('dialog')
      await user.click(within(dialog).getByRole('button', { name: 'Close' }))

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
      expect(trigger).toHaveFocus()
    })

    /*
     * The modal must not imply anything was sent. If a future edit adds a
     * "Sent!" state without an endpoint behind it, this fails.
     */
    it('offers no submit action, because nothing is posted this phase', async () => {
      const user = userEvent.setup()
      renderCard()

      await user.click(
        screen.getByRole('button', { name: 'Request Quote for Premium White Rice' }),
      )

      const dialog = await screen.findByRole('dialog')
      const names = within(dialog)
        .getAllByRole('button')
        .map((button) => button.textContent)
      expect(names).toEqual(['Close'])
    })
  })
})
