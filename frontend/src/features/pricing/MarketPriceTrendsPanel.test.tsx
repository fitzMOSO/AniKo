import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { MarketPriceTrendsPanel } from './MarketPriceTrendsPanel'

describe('MarketPriceTrendsPanel', () => {
  it('names itself and cites its source', () => {
    render(<MarketPriceTrendsPanel />)
    expect(screen.getByText('Market Price Trends')).toBeInTheDocument()
    expect(screen.getByText('Source: AniKo Market Data')).toBeInTheDocument()
  })

  it('legends all three crops above the plot', () => {
    render(<MarketPriceTrendsPanel />)
    const legend = screen.getByTestId('legend')
    for (const crop of ['Rice (White)', 'Corn (Yellow)', 'Vegetables (Mixed)']) {
      expect(legend).toHaveTextContent(crop)
    }
  })

  /*
   * THE GUARD. Recharts renders NOTHING under jsdom when its container measures
   * zero, and it does so silently — no throw, no warning. Every other assertion
   * in this file passes against an empty plot. This one does not.
   *
   * `.recharts-line-curve` is a styling hook rather than a public API, so it is
   * used once, here, and deliberately relied on nowhere else.
   */
  it('actually draws three series, rather than an empty container', () => {
    const { container } = render(<MarketPriceTrendsPanel />)
    expect(container.querySelectorAll('.recharts-line-curve')).toHaveLength(3)
  })

  it('defaults to six months, as the mockup shows', () => {
    render(<MarketPriceTrendsPanel />)
    expect(screen.getByRole('combobox', { name: /price history range/i })).toHaveValue('6')
  })

  it('redraws when the range changes', async () => {
    const user = userEvent.setup()
    const { container } = render(<MarketPriceTrendsPanel />)
    const dots = () => container.querySelectorAll('.recharts-dot').length

    const before = dots()
    await user.selectOptions(
      screen.getByRole('combobox', { name: /price history range/i }),
      '3',
    )
    expect(dots()).toBeLessThan(before)
  })

  it('describes the plot for anyone who cannot see it', () => {
    render(<MarketPriceTrendsPanel />)
    expect(
      screen.getByRole('img', { name: /weekly wholesale price per kilo/i }),
    ).toBeInTheDocument()
  })
})
