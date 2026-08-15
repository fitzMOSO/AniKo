import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { haversineKm } from '@/lib/geo'
import { BUYER_LOCATION } from './fixtures'
import { SupplierMap } from './SupplierMap'
import type { NearbySupplier } from './types'

/*
 * Leaflet is NOT mocked here, deliberately. It runs under jsdom without help —
 * real panes, a real tile <img>, real marker elements — and a mocked
 * react-leaflet would turn every assertion below into a test of the mock. The
 * one thing Leaflet cannot do under jsdom is measure, so nothing here asserts a
 * position: every check is about which suppliers are on the map and how they
 * are reachable, not where their pixels landed.
 */

function nearby(overrides: Partial<NearbySupplier> = {}): NearbySupplier {
  const base = {
    id: 'sup-greenfields',
    name: 'GreenFields Farm',
    region: 'Cabanatuan, Nueva Ecija',
    location: { lat: 15.4869, lng: 120.9675 },
    verified: true,
    crops: ['rice', 'corn'],
    ...overrides,
  } as NearbySupplier
  return { ...base, distanceKm: haversineKm(BUYER_LOCATION, base.location) }
}

const SECOND = nearby({
  id: 'sup-bataan-rice',
  name: 'Bataan Rice Growers',
  region: 'Balanga, Bataan',
  location: { lat: 14.6761, lng: 120.5361 },
  crops: ['rice'],
})

function renderMap(suppliers: NearbySupplier[] = [nearby(), SECOND]) {
  return render(<SupplierMap suppliers={suppliers} origin={BUYER_LOCATION} />)
}

describe('SupplierMap', () => {
  /*
   * THE GUARD, in the same spirit as the Recharts one. Every other assertion in
   * this file would still pass against a map that mounted an empty container,
   * because they query markers that Leaflet appends regardless. This one fails
   * if the tile layer never initialises.
   *
   * `.leaflet-tile` is a styling hook rather than public API, so it is used
   * once, here, and relied on nowhere else.
   */
  it('actually initialises a map, rather than an empty container', () => {
    const { container } = renderMap()
    expect(container.querySelector('.leaflet-tile')).toBeInTheDocument()
  })

  /*
   * The core of the spec: a pin and its row can never disagree.
   * `useNearbySuppliers.test.ts` holds the data half — that the hook filters to
   * verified and sorts nearest-first — and `SupplierList.test.tsx` holds the row
   * half. This is the pin half: exactly the suppliers handed in get a pin.
   */
  it('pins every supplier it is given', () => {
    renderMap()
    expect(screen.getByRole('button', { name: 'GreenFields Farm' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Bataan Rice Growers' })).toBeInTheDocument()
  })

  it('pins no supplier it was not given', () => {
    renderMap([nearby()])
    expect(screen.queryByRole('button', { name: 'Bataan Rice Growers' })).not.toBeInTheDocument()
  })

  /*
   * Counted, not just spot-checked. A map that pinned every supplier in the
   * fixture file rather than the ones the hook approved would still satisfy the
   * two assertions above. `.leaflet-marker-icon` is the only handle Leaflet
   * gives for "all markers", so it is used once, here.
   *
   * The expected count is suppliers + 1: the buyer is on the map too.
   */
  it('draws one pin per supplier and one for the buyer, and no others', () => {
    const { container } = renderMap()
    expect(container.querySelectorAll('.leaflet-marker-icon')).toHaveLength(3)
  })

  it('marks the buyer distinctly from the suppliers', () => {
    renderMap()
    const origin = screen.getByRole('button', { name: 'Your location' })
    expect(origin).toBeInTheDocument()
    // Different shape, not merely a different colour: the buyer's marker is a
    // dot centred on its point, the suppliers' are taller teardrops.
    expect(origin.style.height).not.toBe(
      screen.getByRole('button', { name: 'GreenFields Farm' }).style.height,
    )
  })

  /*
   * The pins are keyboard-reachable buttons with real accessible names, which
   * is the only reason this file can query them by name at all.
   *
   * Worth recording: Leaflet applies a Marker's `alt` option ONLY when the icon
   * element is an <img> (see `Marker._initIcon`). With `divIcon` the element is
   * a <div>, so `alt` is silently dropped and `getByAltText` finds nothing. The
   * accessible name here comes from an sr-only span inside the icon's own HTML
   * instead, with `title` mirroring it for sighted hover.
   */
  it('names each pin for anyone not using a mouse', () => {
    renderMap()
    expect(screen.getByRole('button', { name: 'GreenFields Farm' })).toHaveAttribute(
      'tabindex',
      '0',
    )
  })

  /*
   * Popup children are not in the DOM until the popup opens — Leaflet mounts
   * that content lazily — so this asserts the closed state first to prove the
   * open state is not a false positive.
   */
  it('tells the buyer who a pin is only once they ask', async () => {
    const user = userEvent.setup()
    renderMap()
    expect(screen.queryByText('Cabanatuan, Nueva Ecija')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'GreenFields Farm' }))

    expect(screen.getByText('Cabanatuan, Nueva Ecija')).toBeInTheDocument()
    // The distance in the popup is the one the hook computed, formatted the
    // same way the row formats it. Two numbers for one supplier is the bug.
    expect(screen.getByText(/91 km away/)).toBeInTheDocument()
  })

  it('credits OpenStreetMap, which the licence requires', () => {
    renderMap()
    expect(screen.getByRole('link', { name: 'OpenStreetMap' })).toHaveAttribute(
      'href',
      'https://www.openstreetmap.org/copyright',
    )
  })

  it('labels the map itself, since MapContainer will not carry the label', () => {
    renderMap()
    const map = screen.getByRole('group', { name: 'Map of nearby verified suppliers' })
    expect(within(map).getByRole('button', { name: 'GreenFields Farm' })).toBeInTheDocument()
  })
})
