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
 * Magnitude only — the caller renders an arrow for direction, so a minus sign
 * here would state it twice.
 */
export function formatPercent(value: number, locale: string): string {
  return `${formatCount(Math.abs(value), locale)}%`
}
