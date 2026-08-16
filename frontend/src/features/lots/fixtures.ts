import type { Lot } from './types'

/**
 * The suppliers and provinces here are the same ones in
 * `src/features/suppliers/fixtures.ts`, on purpose: a buyer who reads "Bataan
 * Rice Growers" in the nearby-suppliers list and then again on a lot card is
 * looking at one business, not two coincidentally named ones. Phase I replaces
 * both fixture sets with the same API, so letting them drift now would only
 * hide the join that has to exist later.
 *
 * Order is editorial — "featured" is a merchandising decision, not a ranking
 * anything here can compute. `useFeaturedLots` preserves it rather than
 * inventing a sort.
 *
 * `Bukid Verde Trading` is unverified and deliberately sits in the middle of
 * the run, so the Verified overlay is provably conditional and not something
 * every card gets for free.
 */
export const FEATURED_LOTS: Lot[] = [
  {
    id: 'lot-bataan-white-rice',
    name: 'Premium White Rice',
    crop: 'rice',
    grade: 'A',
    supplier: 'Bataan Rice Growers',
    region: 'Balanga, Bataan',
    verified: true,
    volumeKg: 12000,
    minOrderKg: 500,
    pricePerKg: 52,
  },
  {
    id: 'lot-benguet-highland-mix',
    name: 'Highland Mixed Vegetables',
    crop: 'vegetables',
    grade: 'A',
    supplier: 'Valle Verde Produce',
    region: 'La Trinidad, Benguet',
    verified: true,
    volumeKg: 4200,
    minOrderKg: 200,
    pricePerKg: 68,
  },
  {
    id: 'lot-tarlac-feed-corn',
    name: 'Yellow Feed Corn',
    crop: 'corn',
    grade: 'B',
    supplier: 'Bukid Verde Trading',
    region: 'Tarlac City, Tarlac',
    verified: false,
    volumeKg: 25000,
    minOrderKg: 1000,
    pricePerKg: 23,
  },
  {
    id: 'lot-nueva-ecija-rice',
    name: 'Long Grain Rice',
    crop: 'rice',
    grade: 'B',
    supplier: 'GreenFields Farm',
    region: 'Cabanatuan, Nueva Ecija',
    verified: true,
    volumeKg: 9000,
    minOrderKg: 500,
    pricePerKg: 44,
  },
  {
    id: 'lot-laguna-leafy-greens',
    name: 'Lowland Leafy Greens',
    crop: 'vegetables',
    grade: 'B',
    supplier: 'Laguna Fresh Collective',
    region: 'Calamba, Laguna',
    verified: true,
    volumeKg: 3000,
    minOrderKg: 150,
    pricePerKg: 45,
  },
  {
    id: 'lot-pangasinan-sweet-corn',
    name: 'Sweet Corn',
    crop: 'corn',
    grade: 'A',
    supplier: 'Golden Harvest Co.',
    region: 'Dagupan, Pangasinan',
    verified: true,
    volumeKg: 8000,
    minOrderKg: 400,
    pricePerKg: 35,
  },
]
