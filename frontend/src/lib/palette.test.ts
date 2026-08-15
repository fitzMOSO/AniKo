import { describe, expect, it } from 'vitest'
import { contrastRatio, deltaE2000 } from './color'
import { DELTA, PLOT_BACKGROUND, SERIES, STATUS } from './chart-theme'

const SERIES_MIN_DELTA_E = 20
const GRAPHIC_MIN_CONTRAST = 3.0
const TEXT_MIN_CONTRAST = 4.5
const STATUS_MIN_DELTA_E = 10

/** Confirmed vs Processing is a known, deliberate exception — see chart-theme.ts. */
const ACCEPTED_STATUS_COLLISIONS = new Set(['confirmed|processing'])

describe('chart series', () => {
  const entries = Object.entries(SERIES)

  it.each(entries)('%s is legible against the plot background', (_name, hex) => {
    expect(contrastRatio(hex, PLOT_BACKGROUND)).toBeGreaterThanOrEqual(GRAPHIC_MIN_CONTRAST)
  })

  it('keeps every pair of series perceptually distinct', () => {
    for (let i = 0; i < entries.length; i += 1) {
      for (let j = i + 1; j < entries.length; j += 1) {
        const [aName, a] = entries[i]
        const [bName, b] = entries[j]
        expect(
          deltaE2000(a, b),
          `${aName} (${a}) vs ${bName} (${b}) are too close to tell apart`,
        ).toBeGreaterThanOrEqual(SERIES_MIN_DELTA_E)
      }
    }
  })
})

describe('status badges', () => {
  const entries = Object.entries(STATUS)

  it.each(entries)('%s text is readable on its own fill', (_name, tone) => {
    expect(contrastRatio(tone.text, tone.fill)).toBeGreaterThanOrEqual(TEXT_MIN_CONTRAST)
  })

  it('keeps fills distinguishable, except where explicitly accepted', () => {
    for (let i = 0; i < entries.length; i += 1) {
      for (let j = i + 1; j < entries.length; j += 1) {
        const [aName, a] = entries[i]
        const [bName, b] = entries[j]
        if (ACCEPTED_STATUS_COLLISIONS.has(`${aName}|${bName}`)) continue
        expect(
          deltaE2000(a.fill, b.fill),
          `${aName} and ${bName} fills are the same colour to the eye`,
        ).toBeGreaterThanOrEqual(STATUS_MIN_DELTA_E)
      }
    }
  })
})

describe('delta indicators', () => {
  it.each(Object.entries(DELTA))('%s is legible on the card surface', (_name, hex) => {
    expect(contrastRatio(hex, PLOT_BACKGROUND)).toBeGreaterThanOrEqual(TEXT_MIN_CONTRAST)
  })
})
