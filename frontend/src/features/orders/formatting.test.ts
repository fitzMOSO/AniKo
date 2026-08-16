import { describe, expect, it } from 'vitest'
import { formatDeliveryDate, formatQuantity } from './formatting'

describe('formatDeliveryDate', () => {
  it('renders the PH English form of the date it was given', () => {
    expect(formatDeliveryDate('2026-08-21', 'en')).toBe('Aug 21, 2026')
  })

  it('translates the month when the interface is Filipino', () => {
    // Ago, not Aug — proof the active language reaches Intl rather than a
    // hardcoded format string.
    expect(formatDeliveryDate('2026-08-21', 'fil')).toBe('Ago 21, 2026')
  })

  /*
   * The regression this exists for: without `timeZone: 'UTC'`, an ISO date
   * parsed as UTC midnight renders as the 20th anywhere west of Greenwich.
   * Asserting the day number holds is the cheap, zone-independent version of
   * that check — it fails on any machine where the pin is removed and the
   * offset is negative, and never gives a false pass elsewhere.
   */
  it('does not slip a day when formatted from another timezone', () => {
    expect(formatDeliveryDate('2026-01-01', 'en')).toContain('1')
    expect(formatDeliveryDate('2026-01-01', 'en')).toContain('2026')
    expect(formatDeliveryDate('2026-01-01', 'en')).not.toContain('2025')
  })

  it('falls back to the English locale for a language it does not know', () => {
    expect(formatDeliveryDate('2026-08-21', 'de')).toBe('Aug 21, 2026')
  })
})

describe('formatQuantity', () => {
  it('groups thousands and carries its unit', () => {
    expect(formatQuantity(1500, 'en')).toBe('1,500 kg')
  })

  it('never invents decimal precision', () => {
    expect(formatQuantity(450.4, 'en')).toBe('450 kg')
  })

  it('keeps the unit in Filipino too', () => {
    expect(formatQuantity(2000, 'fil')).toContain('kg')
  })
})
