import { describe, expect, it } from 'vitest'
import { formatVolume } from './volume'

describe('formatVolume', () => {
  it('groups thousands and carries the unit', () => {
    expect(formatVolume(12000, 'en')).toBe('12,000 kg')
  })

  it('rounds to whole kilogrammes, since sacks are not sold by the gram', () => {
    expect(formatVolume(499.6, 'en')).toBe('500 kg')
  })

  it('still formats under an unknown locale rather than throwing', () => {
    expect(formatVolume(1000, 'de')).toBe('1,000 kg')
  })
})
