import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { RECENT_ORDERS } from './fixtures'
import { RecentOrdersPanel } from './RecentOrdersPanel'

function renderPanel(props: { limit?: number } = {}) {
  return render(
    <MemoryRouter>
      <RecentOrdersPanel {...props} />
    </MemoryRouter>,
  )
}

const COLUMNS = ['Order', 'Product', 'Supplier', 'Quantity', 'Status', 'Est. delivery']

describe('RecentOrdersPanel', () => {
  it('titles itself', () => {
    renderPanel()
    expect(screen.getByRole('heading', { name: 'Recent Orders' })).toBeInTheDocument()
  })

  /*
   * A real table, not a grid of divs. Screen-reader users navigating by column
   * get nothing from a div that merely looks like a row, so these queries go
   * through the table roles deliberately — they fail the moment the markup is
   * "simplified" into divs, even if the page still looks identical.
   */
  it('is a table whose columns are named', () => {
    renderPanel()
    const table = screen.getByRole('table')
    const headers = within(table)
      .getAllByRole('columnheader')
      .map((th) => th.textContent)
    expect(headers).toEqual(COLUMNS)
  })

  it('names the table after its heading, so it is not an anonymous table', () => {
    renderPanel()
    expect(screen.getByRole('table', { name: 'Recent Orders' })).toBeInTheDocument()
  })

  it('renders one row per order, plus the header row', () => {
    renderPanel({ limit: 3 })
    expect(screen.getAllByRole('row')).toHaveLength(4)
  })

  it('honours the limit it is given', () => {
    renderPanel({ limit: 2 })
    expect(screen.getAllByRole('rowheader')).toHaveLength(2)
  })

  it('shows everything there is when asked for more than exists', () => {
    renderPanel({ limit: RECENT_ORDERS.length + 10 })
    expect(screen.getAllByRole('rowheader')).toHaveLength(RECENT_ORDERS.length)
  })

  it('carries every field of an order in its row', () => {
    renderPanel({ limit: 1 })
    const [first] = RECENT_ORDERS
    const row = screen.getByRole('row', { name: new RegExp(first.id) })
    expect(within(row).getByText(first.id)).toBeInTheDocument()
    expect(within(row).getByText(first.product)).toBeInTheDocument()
    expect(within(row).getByText(first.supplier)).toBeInTheDocument()
    expect(within(row).getByText('2,000 kg')).toBeInTheDocument()
    expect(within(row).getByText('Processing')).toBeInTheDocument()
    expect(within(row).getByText('Aug 21, 2026')).toBeInTheDocument()
  })

  /*
   * The claim `palette.test.ts` rests on, asserted at the level a reader
   * actually meets it: whatever the status, the word is on screen. Confirmed
   * and Processing are below the perceptual-distance threshold, so a table
   * that showed colour alone would be genuinely ambiguous here.
   */
  it('spells out the status of every row, never colour alone', () => {
    renderPanel({ limit: RECENT_ORDERS.length })
    const words: Record<string, string> = {
      confirmed: 'Confirmed',
      processing: 'Processing',
      shipped: 'Shipped',
      delivered: 'Delivered',
    }
    for (const order of RECENT_ORDERS) {
      const row = screen.getByRole('row', { name: new RegExp(order.id) })
      expect(within(row).getByText(words[order.status])).toBeInTheDocument()
    }
  })

  it('stands in for the missing product photos with initials rather than a broken image', () => {
    renderPanel({ limit: 1 })
    expect(screen.getByText('DR')).toBeInTheDocument()
    // Nothing may point at an image file, because none exist in this repo.
    expect(document.querySelector('img')).toBeNull()
  })

  it('offers a real, focusable route to the full order list', () => {
    renderPanel()
    const link = screen.getByRole('link', { name: 'View all orders' })
    // `/orders` is a routed path in nav.ts — not a placeholder anchor.
    expect(link).toHaveAttribute('href', '/orders')
  })

  it('says plainly when there is nothing to show', () => {
    renderPanel({ limit: 0 })
    expect(screen.getByText('No orders yet.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  /*
   * The other half of the empty state, and the half that usually rots: the
   * message must be gone once rows exist, not merely visually covered.
   */
  it('drops the empty message once there are orders', () => {
    renderPanel({ limit: 3 })
    expect(screen.queryByText('No orders yet.')).not.toBeInTheDocument()
    expect(screen.getByRole('table')).toBeInTheDocument()
  })

  it('keeps the way out of the panel even when the panel is empty', () => {
    renderPanel({ limit: 0 })
    expect(screen.getByRole('link', { name: 'View all orders' })).toBeInTheDocument()
  })

  it('shows five orders when no limit is asked for', () => {
    renderPanel()
    expect(screen.getAllByRole('rowheader')).toHaveLength(5)
  })
})
