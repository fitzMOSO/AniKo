import { describe, expect, it } from 'vitest'
import { formatCount, formatCurrency, formatDistance, formatPercent } from './format'

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

describe('formatDistance', () => {
  it('rounds to whole kilometres', () => {
    expect(formatDistance(90.47, 'en')).toBe('90 km')
    expect(formatDistance(90.5, 'en')).toBe('91 km')
    expect(formatDistance(53.3, 'en')).toBe('53 km')
  })

  it('carries the unit under the Filipino locale too', () => {
    expect(formatDistance(203.7, 'fil')).toContain('km')
  })

  // Straight-line distances between municipal centres do not deserve decimals;
  // showing "90.5 km" would imply a precision the number does not have.
  it('never shows a fractional kilometre', () => {
    expect(formatDistance(90.47, 'en')).not.toContain('.')
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
