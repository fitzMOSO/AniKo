/**
 * All number formatting for the dashboard, in one place and locale-aware.
 *
 * The app's i18n languages are `en` and `fil`; both map to a PH region because
 * the product is Philippine regardless of the interface language. The mockup's
 * US dollars and Californian cities are placeholder data — the spec and both
 * checklists specify PH regions and pesos.
 */

const REGION: Record<string, string> = {
  en: 'en-PH',
  fil: 'fil-PH',
}

function resolve(locale: string): string {
  return REGION[locale] ?? REGION.en
}

/** Whole counts with thousands separators. */
export function formatCount(value: number, locale: string): string {
  return new Intl.NumberFormat(resolve(locale), { maximumFractionDigits: 0 }).format(value)
}

/**
 * Pesos, no centavos. Dashboard figures are read at a glance, and two decimal
 * places on a seven-figure total is noise.
 */
export function formatCurrency(value: number, locale: string): string {
  return new Intl.NumberFormat(resolve(locale), {
    style: 'currency',
    currency: 'PHP',
    maximumFractionDigits: 0,
  }).format(value)
}

/**
 * Whole kilometres, via `Intl` rather than a hardcoded " km" suffix so the unit
 * is placed and spaced the way each locale expects. Fractional precision would
 * be false confidence: these are straight-line distances between municipal
 * centres, not road distances.
 */
export function formatDistance(km: number, locale: string): string {
  return new Intl.NumberFormat(resolve(locale), {
    style: 'unit',
    unit: 'kilometer',
    unitDisplay: 'short',
    maximumFractionDigits: 0,
  }).format(km)
}

/**
 * Whole kilogrammes, for both lot volumes and order quantities.
 *
 * One function rather than two: Phases F and G independently wrote
 * `formatVolume` and `formatQuantity` with byte-identical bodies. "Volume" and
 * "quantity" are different words for the same measurement here, and two names
 * for one behaviour is how the two drift apart later.
 *
 * Whole kilos, no decimals: wholesale lots are quoted in sacks, cavans and
 * tonnes, so a decimal place on a 12,000 kg lot is precision the seller never
 * offered.
 */
export function formatWeight(kg: number, locale: string): string {
  return new Intl.NumberFormat(resolve(locale), {
    style: 'unit',
    unit: 'kilogram',
    unitDisplay: 'short',
    maximumFractionDigits: 0,
  }).format(kg)
}

/**
 * A calendar date from an ISO `YYYY-MM-DD` string.
 *
 * `timeZone: 'UTC'` is not optional. A date-only ISO string parses as UTC
 * midnight, so formatting it in any negative-offset zone renders the *previous
 * day* — a delivery estimate that silently slips depending on where the browser
 * is. Every date in this codebase is pinned to UTC for that reason.
 *
 * Month is abbreviated, never numeric: `08/09` is the 9th of August in Manila
 * and the 8th of September to half the readers of the same table.
 */
export function formatDate(iso: string, locale: string): string {
  return new Intl.DateTimeFormat(resolve(locale), {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(`${iso}T00:00:00Z`))
}

/**
 * Magnitude only — the caller renders an arrow for direction, so a minus sign
 * here would state it twice.
 */
export function formatPercent(value: number, locale: string): string {
  return `${formatCount(Math.abs(value), locale)}%`
}
