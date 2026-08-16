import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { STATUS, type StatusKey } from '@/lib/chart-theme'
import { OrderStatusBadge } from './OrderStatusBadge'

const STATUSES = Object.keys(STATUS) as StatusKey[]

const WORDS: Record<StatusKey, string> = {
  confirmed: 'Confirmed',
  processing: 'Processing',
  shipped: 'Shipped',
  delivered: 'Delivered',
}

describe('OrderStatusBadge', () => {
  /*
   * This is the test that backs the accepted exemption in `palette.test.ts`:
   * Confirmed and Processing sit at dE 8.73, below the distinctness threshold,
   * and that is only acceptable because the word is always in the DOM. If a
   * future refactor turns the badge into a swatch, this fails before anyone
   * ships a table where two statuses are indistinguishable.
   */
  it.each(STATUSES)('always renders the word for %s, never colour alone', (status) => {
    render(<OrderStatusBadge status={status} />)
    expect(screen.getByText(WORDS[status])).toBeInTheDocument()
  })

  it('gives every status a visible label, so no status renders empty', () => {
    for (const status of STATUSES) {
      const { container, unmount } = render(<OrderStatusBadge status={status} />)
      expect(container.textContent?.trim()).not.toBe('')
      // A missing catalogue entry would surface as the raw key, which reads as
      // text and would otherwise sail past the assertion above.
      expect(container.textContent).not.toContain('orders.status_')
      unmount()
    }
  })

  it('takes its colour from the shared palette rather than a local literal', () => {
    render(<OrderStatusBadge status="shipped" />)
    const badge = screen.getByText('Shipped')
    expect(badge).toHaveStyle({ backgroundColor: STATUS.shipped.fill })
    expect(badge).toHaveStyle({ color: STATUS.shipped.text })
  })

  it('distinguishes the two statuses the palette cannot, by their text', () => {
    const { unmount } = render(<OrderStatusBadge status="confirmed" />)
    expect(screen.getByText('Confirmed')).toBeInTheDocument()
    unmount()
    render(<OrderStatusBadge status="processing" />)
    expect(screen.getByText('Processing')).toBeInTheDocument()
    expect(screen.queryByText('Confirmed')).not.toBeInTheDocument()
  })
})
