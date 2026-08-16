import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { LotsScroller } from './LotsScroller'
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

const EMPTY_MESSAGE = 'No featured lots right now.'

describe('LotsScroller', () => {
  it('renders one card per lot', () => {
    render(<LotsScroller lots={[lot(), lot({ id: 'b', name: 'Sweet Corn' })]} />)
    expect(screen.getAllByRole('listitem')).toHaveLength(2)
  })

  it('preserves the order it is given, rather than re-sorting', () => {
    render(
      <LotsScroller
        lots={[lot({ id: 'b', name: 'Sweet Corn' }), lot({ id: 'a', name: 'Long Grain Rice' })]}
      />,
    )
    const cards = screen.getAllByRole('listitem').map((li) => li.textContent)
    expect(cards[0]).toContain('Sweet Corn')
    expect(cards[1]).toContain('Long Grain Rice')
  })

  /*
   * A scrollable box is not focusable by default, so without this a keyboard
   * user can never scroll the strip and the cards past the fold do not exist
   * for them. The name is equally load-bearing: an unnamed region is dropped
   * from the landmark list altogether.
   */
  describe('keyboard reach', () => {
    it('exposes a named region', () => {
      render(<LotsScroller lots={[lot()]} />)
      expect(screen.getByRole('region', { name: 'Featured wholesale lots' })).toBeInTheDocument()
    })

    it('puts the strip itself in the tab order, ahead of the cards', async () => {
      const user = userEvent.setup()
      render(<LotsScroller lots={[lot()]} />)

      const region = screen.getByRole('region', { name: 'Featured wholesale lots' })
      expect(region).toHaveAttribute('tabindex', '0')

      await user.tab()
      expect(region).toHaveFocus()
    })

    it('reaches every card control by tabbing, with nothing trapped', async () => {
      const user = userEvent.setup()
      render(<LotsScroller lots={[lot(), lot({ id: 'b', name: 'Sweet Corn' })]} />)

      await user.tab() // region
      await user.tab()
      expect(screen.getByRole('button', { name: 'Save Premium White Rice' })).toHaveFocus()
      await user.tab()
      await user.tab()
      expect(screen.getByRole('button', { name: 'Save Sweet Corn' })).toHaveFocus()
    })
  })

  /*
   * The classic failure mode: a carousel that clamps `touch-action` or calls
   * `preventDefault` on touchmove swallows the vertical swipe too, stranding
   * the reader mid-page. Scrolling here is CSS on one axis and nothing else,
   * so the browser keeps the vertical gesture.
   */
  describe('touch scroll', () => {
    it('does not swallow a touch gesture', () => {
      render(<LotsScroller lots={[lot()]} />)
      const region = screen.getByRole('region', { name: 'Featured wholesale lots' })

      const moved = fireEvent.touchMove(region, { touches: [{ clientX: 0, clientY: 40 }] })
      // fireEvent returns false only when a handler called preventDefault.
      expect(moved).toBe(true)
    })

    it('scrolls on the horizontal axis only, and never clamps touch-action', () => {
      render(<LotsScroller lots={[lot()]} />)
      const region = screen.getByRole('region', { name: 'Featured wholesale lots' })

      expect(region.className).toContain('overflow-x-auto')
      expect(region.className).not.toContain('touch-none')
      expect(region.className).not.toContain('overflow-y-hidden')
    })
  })

  describe('empty state', () => {
    it('says so plainly when there is nothing to feature', () => {
      render(<LotsScroller lots={[]} />)
      expect(screen.getByText(EMPTY_MESSAGE)).toBeInTheDocument()
      expect(screen.queryByRole('region')).not.toBeInTheDocument()
      expect(screen.queryByRole('list')).not.toBeInTheDocument()
    })

    /* The cross-cutting checklist item: the message must not linger. */
    it('drops the message as soon as there is a lot to show', () => {
      const { rerender } = render(<LotsScroller lots={[]} />)
      expect(screen.getByText(EMPTY_MESSAGE)).toBeInTheDocument()

      rerender(<LotsScroller lots={[lot()]} />)
      expect(screen.queryByText(EMPTY_MESSAGE)).not.toBeInTheDocument()
      expect(screen.getAllByRole('listitem')).toHaveLength(1)
    })
  })
})
