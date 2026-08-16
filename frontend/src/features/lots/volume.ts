/**
 * Kilogramme formatting for lot volumes.
 *
 * This belongs next to `formatCount` / `formatCurrency` / `formatDistance` in
 * `@/lib/format`, and should be moved there the next time that file is open —
 * it is here only because Phase F does not own `src/lib`. The duplication is
 * the region map below, and it is deliberate rather than accidental: see the
 * note on it.
 *
 * Via `Intl` rather than a hardcoded " kg" suffix, for the same reason
 * `formatDistance` is: the unit's placement and spacing are a locale decision,
 * not ours.
 */

/*
 * Mirrors `REGION` in `@/lib/format`. Both interface languages map to a PH
 * region because the product is Philippine regardless of the language it is
 * read in — a lot measured in kg does not become a different lot in Filipino.
 */
const REGION: Record<string, string> = {
  en: 'en-PH',
  fil: 'fil-PH',
}

/**
 * Whole kilogrammes. Wholesale lots are quoted in sacks and tonnes; a decimal
 * place on a 12,000 kg lot is precision the seller never offered.
 */
export function formatVolume(kg: number, locale: string): string {
  return new Intl.NumberFormat(REGION[locale] ?? REGION.en, {
    style: 'unit',
    unit: 'kilogram',
    unitDisplay: 'short',
    maximumFractionDigits: 0,
  }).format(kg)
}
