import { describe, expect, it } from 'vitest'
import {
  formatCount,
  formatCurrency,
  formatDate,
  formatDistance,
  formatPercent,
  formatWeight,
} from './format'

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

/*
 * Merged from what were `features/lots/volume.test.ts` and
 * `features/orders/formatting.test.ts`. Two phases wrote the same kilogramme
 * formatter under two names; these are both sets of cases, kept whole, because
 * each phase had found an edge the other had not.
 */
describe('formatWeight', () => {
  it('groups thousands and carries its unit', () => {
    expect(formatWeight(12000, 'en')).toBe('12,000 kg')
    expect(formatWeight(1500, 'en')).toBe('1,500 kg')
  })

  it('rounds to whole kilogrammes, since sacks are not sold by the gram', () => {
    expect(formatWeight(499.6, 'en')).toBe('500 kg')
    expect(formatWeight(450.4, 'en')).toBe('450 kg')
  })

  it('keeps the unit in Filipino too', () => {
    expect(formatWeight(2000, 'fil')).toContain('kg')
  })

  it('still formats under an unknown locale rather than throwing', () => {
    expect(formatWeight(1000, 'de')).toBe('1,000 kg')
  })
})

describe('formatDate', () => {
  it('renders the PH English form of the date it was given', () => {
    expect(formatDate('2026-08-21', 'en')).toBe('Aug 21, 2026')
  })

  it('translates the month when the interface is Filipino', () => {
    // Ago, not Aug — proof the active language reaches Intl rather than a
    // hardcoded format string.
    expect(formatDate('2026-08-21', 'fil')).toBe('Ago 21, 2026')
  })

  /*
   * The regression this exists for: without `timeZone: 'UTC'`, an ISO date
   * parsed as UTC midnight renders as the previous day anywhere west of
   * Greenwich. Asserting the day and year hold is the zone-independent version
   * of that check — it fails wherever the pin is removed under a negative
   * offset, and never gives a false pass elsewhere.
   */
  it('does not slip a day when formatted from another timezone', () => {
    expect(formatDate('2026-01-01', 'en')).toContain('1')
    expect(formatDate('2026-01-01', 'en')).toContain('2026')
    expect(formatDate('2026-01-01', 'en')).not.toContain('2025')
  })

  it('falls back to the English locale for a language it does not know', () => {
    expect(formatDate('2026-08-21', 'de')).toBe('Aug 21, 2026')
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
