import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StatTilesRow } from './StatTilesRow'

describe('StatTilesRow', () => {
  it('renders every Buyer tile, in order', () => {
    render(<StatTilesRow />)
    const tiles = screen.getAllByRole('article').map((el) => el.textContent)
    expect(tiles).toHaveLength(4)
    expect(tiles[0]).toContain('New Inquiries')
    expect(tiles[3]).toContain('Spend This Month')
  })
})
