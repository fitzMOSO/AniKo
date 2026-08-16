import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { FeaturedLotsPanel } from './FeaturedLotsPanel'
import { FEATURED_LOTS } from './fixtures'

describe('FeaturedLotsPanel', () => {
  it('names itself', () => {
    render(<FeaturedLotsPanel />)
    expect(
      screen.getByRole('heading', { name: 'Featured Wholesale Lots', level: 2 }),
    ).toBeInTheDocument()
  })

  it('shows every featured lot the hook returns', () => {
    render(<FeaturedLotsPanel />)
    expect(screen.getAllByRole('listitem')).toHaveLength(FEATURED_LOTS.length)
    for (const lot of FEATURED_LOTS) {
      expect(screen.getByRole('heading', { name: lot.name })).toBeInTheDocument()
    }
  })

  it('puts them in a keyboard-reachable, named strip', () => {
    render(<FeaturedLotsPanel />)
    const region = screen.getByRole('region', { name: 'Featured wholesale lots' })
    expect(region).toHaveAttribute('tabindex', '0')
  })

  /* Every card carries its own quote trigger, named for its own lot. */
  it('gives each lot its own Request Quote trigger', () => {
    render(<FeaturedLotsPanel />)
    for (const lot of FEATURED_LOTS) {
      expect(
        screen.getByRole('button', { name: `Request a quote for ${lot.name}` }),
      ).toBeInTheDocument()
    }
  })

  it('does not show the empty message when there are lots', () => {
    render(<FeaturedLotsPanel />)
    expect(screen.queryByText('No featured lots right now.')).not.toBeInTheDocument()
  })
})
