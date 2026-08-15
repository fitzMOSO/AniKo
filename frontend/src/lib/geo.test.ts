import { describe, expect, it } from 'vitest'
import { haversineKm, type LatLng } from './geo'

const MANILA: LatLng = { lat: 14.5995, lng: 120.9842 }
const CEBU: LatLng = { lat: 10.3157, lng: 123.8854 }
const DAVAO: LatLng = { lat: 7.1907, lng: 125.4553 }
const LONDON: LatLng = { lat: 51.5074, lng: -0.1278 }
const PARIS: LatLng = { lat: 48.8566, lng: 2.3522 }

describe('haversineKm', () => {
  /*
   * Checked against published great-circle distances rather than against
   * whatever this implementation happens to return. A distance function that
   * only agrees with itself is not validated — it is just consistent.
   */
  it.each([
    ['Manila to Cebu', MANILA, CEBU, 570],
    ['Manila to Davao', MANILA, DAVAO, 960],
    ['London to Paris', LONDON, PARIS, 344],
  ] as const)('measures %s within 1% of the published distance', (_label, a, b, expected) => {
    expect(Math.abs(haversineKm(a, b) - expected) / expected).toBeLessThan(0.01)
  })

  it('is zero for a point against itself', () => {
    expect(haversineKm(MANILA, MANILA)).toBe(0)
  })

  it('is symmetric', () => {
    expect(haversineKm(MANILA, CEBU)).toBeCloseTo(haversineKm(CEBU, MANILA), 9)
  })

  // Longitude degrees converge toward the poles. A naive sqrt(dLat^2+dLng^2)
  // misses this entirely, so it is asserted rather than assumed.
  it('accounts for meridian convergence away from the equator', () => {
    const equator = haversineKm({ lat: 0, lng: 0 }, { lat: 0, lng: 1 })
    const high = haversineKm({ lat: 60, lng: 0 }, { lat: 60, lng: 1 })
    expect(high).toBeLessThan(equator / 1.9)
  })
})
