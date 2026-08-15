import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { haversineKm } from '@/lib/geo'
import { BUYER_LOCATION } from './fixtures'
import { SupplierList } from './SupplierList'
import type { NearbySupplier } from './types'

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

describe('SupplierList', () => {
  it('names each supplier and where it is', () => {
    render(<SupplierList suppliers={[nearby()]} />)
    expect(screen.getByText('GreenFields Farm')).toBeInTheDocument()
    expect(screen.getByText('Cabanatuan, Nueva Ecija')).toBeInTheDocument()
  })

  /*
   * The distance shown must be the distance the hook computed — not a value
   * the row rounds, recomputes, or invents. This is the row half of the "a pin
   * and its row can never disagree" guarantee; `useNearbySuppliers.test.ts`
   * holds the other half.
   */
  it('shows the distance it was handed, in whole kilometres', () => {
    render(<SupplierList suppliers={[nearby()]} />)
    // Cabanatuan is 90.5 km from the buyer, which rounds up.
    expect(screen.getByText(/91 km away/)).toBeInTheDocument()
  })

  it('marks every supplier verified, since unverified ones never reach here', () => {
    render(<SupplierList suppliers={[nearby(), nearby({ id: 'b', name: 'Second Farm' })]} />)
    expect(screen.getAllByText('Verified')).toHaveLength(2)
  })

  it('tags the crops a supplier actually grows, and no others', () => {
    render(<SupplierList suppliers={[nearby({ crops: ['rice'] })]} />)
    const row = screen.getByRole('listitem')
    expect(within(row).getByText('Rice')).toBeInTheDocument()
    expect(within(row).queryByText('Corn')).not.toBeInTheDocument()
  })

  /*
   * No photo assets exist and none should be invented, so the thumbnail is
   * initials. It degrades to nothing on the poor connections the spec cares
   * about, which is the point.
   */
  it('falls back to initials rather than a broken image', () => {
    render(<SupplierList suppliers={[nearby()]} />)
    expect(screen.getByText('GF')).toBeInTheDocument()
  })

  it('preserves the order it is given, rather than re-sorting', () => {
    render(
      <SupplierList
        suppliers={[
          nearby({ id: 'far', name: 'Far Farm', location: { lat: 16.455, lng: 120.5887 } }),
          nearby({ id: 'near', name: 'Near Farm' }),
        ]}
      />,
    )
    const names = screen.getAllByRole('listitem').map((li) => li.textContent)
    expect(names[0]).toContain('Far Farm')
    expect(names[1]).toContain('Near Farm')
  })

  it('says so plainly when there is nothing to list', () => {
    render(<SupplierList suppliers={[]} />)
    expect(screen.getByText('No verified suppliers within range yet.')).toBeInTheDocument()
    expect(screen.queryByRole('list')).not.toBeInTheDocument()
  })
})
