import type { LatLng } from '@/lib/geo'
import type { Supplier } from './types'

/**
 * Where the buyer is, and therefore where distances are measured from and where
 * the map centres. Phase I takes this from the account's delivery address; it
 * is a constant here so distances are reproducible in tests.
 */
export const BUYER_LOCATION: LatLng = { lat: 14.676, lng: 121.0437 } // Quezon City

/**
 * The mockup's supplier names are brand-neutral and worth keeping, but its
 * geography is Californian — San Jose, Salinas, Fresno — against a spec that is
 * explicitly Philippine. Same call as the peso/dollar conflict in Phase C: the
 * spec wins, so the names stay and the places are real PH farming provinces.
 *
 * Coordinates are the municipal centres. `Bukid Verde` is deliberately
 * unverified: the panel promises verified suppliers, and a filter with nothing
 * to filter is a claim no test can check.
 */
export const ALL_SUPPLIERS: Supplier[] = [
  {
    id: 'sup-laguna-fresh',
    name: 'Laguna Fresh Collective',
    region: 'Calamba, Laguna',
    location: { lat: 14.2117, lng: 121.1653 },
    verified: true,
    crops: ['vegetables'],
  },
  {
    id: 'sup-bataan-rice',
    name: 'Bataan Rice Growers',
    region: 'Balanga, Bataan',
    location: { lat: 14.6761, lng: 120.5361 },
    verified: true,
    crops: ['rice'],
  },
  {
    id: 'sup-greenfields',
    name: 'GreenFields Farm',
    region: 'Cabanatuan, Nueva Ecija',
    location: { lat: 15.4869, lng: 120.9675 },
    verified: true,
    crops: ['rice', 'corn'],
  },
  {
    id: 'sup-bukid-verde',
    name: 'Bukid Verde Trading',
    region: 'Tarlac City, Tarlac',
    location: { lat: 15.4755, lng: 120.5963 },
    verified: false,
    crops: ['corn'],
  },
  {
    id: 'sup-golden-harvest',
    name: 'Golden Harvest Co.',
    region: 'Dagupan, Pangasinan',
    location: { lat: 16.0433, lng: 120.3333 },
    verified: true,
    crops: ['corn', 'rice'],
  },
  {
    id: 'sup-valle-verde',
    name: 'Valle Verde Produce',
    region: 'La Trinidad, Benguet',
    location: { lat: 16.455, lng: 120.5887 },
    verified: true,
    crops: ['vegetables'],
  },
]
