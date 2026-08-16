/**
 * Locale-aware formatting for the two order fields `@/lib/format` does not
 * cover yet: a delivery date and a weight.
 *
 * The `en -> en-PH` / `fil -> fil-PH` mapping is repeated from `@/lib/format`
 * rather than imported because that module does not export it. It is repeated
 * exactly, and both helpers below are tested against both languages, so a
 * divergence shows up as a failing test rather than as a date that quietly
 * formats US-style. Promoting these two into `@/lib/format` (and dropping this
 * file) is the right follow-up once that file is free to change.
 */
const REGION: Record<string, string> = {
  en: 'en-PH',
  fil: 'fil-PH',
}

function resolve(locale: string): string {
  return REGION[locale] ?? REGION.en
}

/**
 * `timeZone: 'UTC'` is not optional. An ISO date with no time part is parsed as
 * UTC midnight, so formatting it in any negative-offset zone renders the
 * previous day — a delivery estimate that slips a day depending on where the
 * browser happens to be. The rest of the codebase pins UTC for the same reason.
 *
 * Month is abbreviated, not numeric: `08/09` is the ninth of August in Manila
 * and the eighth of September to half the readers of the same table.
 */
export function formatDeliveryDate(iso: string, locale: string): string {
  return new Intl.DateTimeFormat(resolve(locale), {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(`${iso}T00:00:00Z`))
}

/**
 * Kilograms via `Intl`'s unit style rather than a hardcoded " kg" suffix, so
 * the unit is placed and spaced the way each locale expects — the same call
 * `formatDistance` makes for kilometres. Whole kilograms: order volumes are
 * agreed in sacks and cavans, and a decimal here would be invented precision.
 */
export function formatQuantity(kg: number, locale: string): string {
  return new Intl.NumberFormat(resolve(locale), {
    style: 'unit',
    unit: 'kilogram',
    unitDisplay: 'short',
    maximumFractionDigits: 0,
  }).format(kg)
}
