/**
 * Great-circle distance, with no dependency and no map library involved.
 *
 * Distance is computed from coordinates rather than stored alongside them.
 * Storing both invites the two to drift apart, and the spec is explicit that a
 * pin and its list row must never disagree — so there is exactly one source of
 * truth for where a supplier is, and the distance is derived from it.
 */

export interface LatLng {
  lat: number
  lng: number
}

/** Mean Earth radius in kilometres (IUGG). */
const EARTH_RADIUS_KM = 6371

const toRadians = (degrees: number) => (degrees * Math.PI) / 180

/**
 * Haversine rather than the equirectangular approximation: the Philippines
 * spans roughly 5°N to 21°N, far enough from the equator that treating a
 * degree of longitude as a fixed distance would visibly misreport distances in
 * the north of Luzon.
 */
export function haversineKm(a: LatLng, b: LatLng): number {
  const dLat = toRadians(b.lat - a.lat)
  const dLng = toRadians(b.lng - a.lng)

  const h =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRadians(a.lat)) * Math.cos(toRadians(b.lat)) * Math.sin(dLng / 2) ** 2

  return 2 * EARTH_RADIUS_KM * Math.asin(Math.sqrt(h))
}
