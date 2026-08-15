import { render, screen } from '@testing-library/react'
import { ShoppingCart } from 'lucide-react'
import { describe, expect, it } from 'vitest'
import { DELTA } from '@/lib/chart-theme'
import type { OverviewStat } from './types'
import { StatTile } from './StatTile'

function stat(overrides: Partial<OverviewStat> = {}): OverviewStat {
  return {
    key: 'pending_orders',
    labelKey: 'stats.pending_orders',
    icon: ShoppingCart,
    value: 9,
    format: 'count',
    deltaPercent: 6,
    upIsGood: true,
    ...overrides,
  }
}

/** The rendered delta colour, read off the element's inline style. */
function deltaColour(): string {
  return screen.getByTestId('delta').style.color
}

function rgb(hex: string): string {
  const n = hex.replace('#', '')
  const [r, g, b] = [0, 2, 4].map((i) => parseInt(n.slice(i, i + 2), 16))
  return `rgb(${r}, ${g}, ${b})`
}

describe('StatTile', () => {
  it('shows the label and the formatted value', () => {
    render(<StatTile stat={stat({ value: 48760 })} />)
    expect(screen.getByText('Pending Orders')).toBeInTheDocument()
    expect(screen.getByText('48,760')).toBeInTheDocument()
  })

  it('formats a currency tile in pesos', () => {
    render(<StatTile stat={stat({ format: 'currency', value: 2671400 })} />)
    expect(screen.getByTestId('value').textContent).toContain('₱')
  })

  // --- the delta rule: arrow follows the sign, colour follows the meaning ---

  it('a rise on an up-is-good tile is green and points up', () => {
    render(<StatTile stat={stat({ deltaPercent: 12, upIsGood: true })} />)
    expect(deltaColour()).toBe(rgb(DELTA.up))
    expect(screen.getByLabelText(/up 12% versus last month/i)).toBeInTheDocument()
  })

  it('a fall on an up-is-good tile is red and points down', () => {
    render(<StatTile stat={stat({ deltaPercent: -12, upIsGood: true })} />)
    expect(deltaColour()).toBe(rgb(DELTA.down))
    expect(screen.getByLabelText(/down 12% versus last month/i)).toBeInTheDocument()
  })

  it('a fall on an up-is-bad tile is GREEN, though it points down', () => {
    // Fewer pending orders means orders are being fulfilled. This is the case
    // a sign-driven implementation gets wrong.
    render(<StatTile stat={stat({ deltaPercent: -2, upIsGood: false })} />)
    expect(deltaColour()).toBe(rgb(DELTA.up))
    expect(screen.getByLabelText(/down 2% versus last month/i)).toBeInTheDocument()
  })

  it('a rise on an up-is-bad tile is RED, though it points up', () => {
    // Spending more is not a success.
    render(<StatTile stat={stat({ deltaPercent: 18, upIsGood: false })} />)
    expect(deltaColour()).toBe(rgb(DELTA.down))
    expect(screen.getByLabelText(/up 18% versus last month/i)).toBeInTheDocument()
  })

  it('renders an unchanged delta as neither good nor bad', () => {
    render(<StatTile stat={stat({ deltaPercent: 0 })} />)
    expect(deltaColour()).toBe('')
    expect(screen.getByLabelText(/unchanged versus last month/i)).toBeInTheDocument()
  })

  it('states the comparison in words, because a bare arrow is a guess', () => {
    render(<StatTile stat={stat()} />)
    expect(screen.getByText(/vs last month/i)).toBeInTheDocument()
  })
})
