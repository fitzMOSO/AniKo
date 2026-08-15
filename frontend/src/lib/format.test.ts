import { describe, expect, it } from 'vitest'
import { formatCount, formatCurrency, formatPercent } from './format'

describe('formatCount', () => {
  it('groups thousands', () => {
    expect(formatCount(48760, 'en')).toBe('48,760')
  })

  it('leaves small numbers alone', () => {
    expect(formatCount(9, 'en')).toBe('9')
  })
})

describe('formatCurrency', () => {
  it("renders pesos, not dollars — the mockup's $ is US placeholder data", () => {
    expect(formatCurrency(48760, 'en')).toContain('₱')
    expect(formatCurrency(48760, 'en')).not.toContain('$')
  })

  it('groups thousands and shows no centavos', () => {
    expect(formatCurrency(48760, 'en')).toContain('48,760')
    expect(formatCurrency(48760, 'en')).not.toContain('.00')
  })

  it('still renders pesos under the Filipino locale', () => {
    expect(formatCurrency(48760, 'fil')).toContain('₱')
  })
})

describe('formatPercent', () => {
  it('renders the magnitude only — the arrow carries direction', () => {
    expect(formatPercent(12, 'en')).toBe('12%')
    expect(formatPercent(-2, 'en')).toBe('2%')
  })

  it('handles zero', () => {
    expect(formatPercent(0, 'en')).toBe('0%')
  })
})
