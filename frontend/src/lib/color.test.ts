import { describe, expect, it } from 'vitest'
import { contrastRatio, deltaE2000 } from './color'

describe('contrastRatio', () => {
  it('returns 21:1 for black on white', () => {
    expect(contrastRatio('#000000', '#FFFFFF')).toBeCloseTo(21, 1)
  })

  it('returns 1:1 for a colour against itself', () => {
    expect(contrastRatio('#3A942A', '#3A942A')).toBeCloseTo(1, 5)
  })

  it('is symmetric — order of arguments must not matter', () => {
    expect(contrastRatio('#FCAE11', '#FFFFFF')).toBeCloseTo(
      contrastRatio('#FFFFFF', '#FCAE11'),
      5,
    )
  })

  it('rejects anything that is not a #RRGGBB colour', () => {
    expect(() => contrastRatio('rebeccapurple', '#FFFFFF')).toThrow(/#RRGGBB/)
  })
})

describe('deltaE2000', () => {
  it('is zero for identical colours', () => {
    expect(deltaE2000('#3A942A', '#3A942A')).toBeCloseTo(0, 5)
  })

  it('matches the published CIEDE2000 value for white vs black', () => {
    // L*=100 vs L*=0, with both chromas zero, reduces to dL/SL where SL = 1.
    expect(deltaE2000('#FFFFFF', '#000000')).toBeCloseTo(100, 0)
  })

  it('separates the mockup greens by more than the 20-unit threshold', () => {
    expect(deltaE2000('#3A942A', '#09410E')).toBeGreaterThan(20)
  })

  it('is symmetric', () => {
    expect(deltaE2000('#E9F3E5', '#C7E5C3')).toBeCloseTo(
      deltaE2000('#C7E5C3', '#E9F3E5'),
      5,
    )
  })
})
