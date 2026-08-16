import { render as rtlRender, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { ALL_SUPPLIERS } from './fixtures'
import { NearbySuppliersPanel } from './NearbySuppliersPanel'

// Rows link to supplier profiles, so the panel needs a Router in scope.
const render = (ui: React.ReactElement) => rtlRender(<MemoryRouter>{ui}</MemoryRouter>)

const VERIFIED = ALL_SUPPLIERS.filter((s) => s.verified)
const UNVERIFIED = ALL_SUPPLIERS.filter((s) => !s.verified)

describe('NearbySuppliersPanel', () => {
  it('names itself', () => {
    render(<NearbySuppliersPanel />)
    expect(
      screen.getByRole('heading', { name: 'Nearby Verified Suppliers' }),
    ).toBeInTheDocument()
  })

  /*
   * The list is the primary interface and must not wait on the map. The map is
   * behind `lazy()`, so on this first synchronous paint its chunk has not
   * resolved — which makes this render the exact state a slow connection sees.
   * Every supplier is already listed in it.
   */
  it('lists every verified supplier before the map chunk has loaded', () => {
    render(<NearbySuppliersPanel />)
    expect(screen.queryByRole('group', { name: /map of nearby/i })).not.toBeInTheDocument()

    const list = screen.getByRole('list', { name: 'Verified suppliers, nearest first' })
    expect(within(list).getAllByRole('listitem')).toHaveLength(VERIFIED.length)
    for (const supplier of VERIFIED) {
      expect(within(list).getByText(supplier.name)).toBeInTheDocument()
    }
    // And no "nothing here" line alongside the rows it does have.
    expect(screen.queryByText('No verified suppliers within range yet.')).not.toBeInTheDocument()
  })

  it('holds the space the map will take, rather than reflowing the list later', () => {
    render(<NearbySuppliersPanel />)
    expect(screen.getByRole('status')).toHaveTextContent('Loading the supplier map…')
  })

  /*
   * The guarantee this panel exists to make: one hook call feeds both children,
   * so the set of pins and the set of rows are the same set. Checked against the
   * fixture's deliberately unverified supplier, which must appear in neither.
   */
  it('pins exactly the suppliers it lists, once the map arrives', async () => {
    const { container } = render(<NearbySuppliersPanel />)
    const map = await screen.findByRole('group', { name: /map of nearby/i })

    const rows = screen.getAllByRole('listitem')
    // + 1 for the buyer's own marker, which is not a supplier.
    expect(container.querySelectorAll('.leaflet-marker-icon')).toHaveLength(rows.length + 1)

    for (const supplier of VERIFIED) {
      expect(within(map).getByRole('button', { name: supplier.name })).toBeInTheDocument()
    }
  })

  it('neither lists nor pins an unverified supplier', async () => {
    render(<NearbySuppliersPanel />)
    await screen.findByRole('group', { name: /map of nearby/i })

    expect(UNVERIFIED.length).toBeGreaterThan(0)
    for (const supplier of UNVERIFIED) {
      expect(screen.queryByText(supplier.name)).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: supplier.name })).not.toBeInTheDocument()
    }
  })
})
