/**
 * Regenerates the favicon and PWA icons for AniKo from its own lockup.
 *
 * sharp is not a dependency of this project — it is only needed when the icons
 * change, which is rarely:
 *
 *   npm i -D sharp --no-save && node scripts/generate-icons.mjs && rm -rf node_modules/sharp
 *
 * `rm -rf` rather than `npm uninstall`: uninstall rewrites package.json even
 * when the install was --no-save, re-sorting unrelated dependencies into a
 * diff you did not ask for.
 *
 * ## Why this script exists
 *
 * public/brand/aniko-logo.png is a real designed lockup — glyph on the left,
 * "AniKo" wordmark on the right — but nothing had ever cut an icon from it, so
 * favicon.svg was still the FitzDev Portfolio's purple bolt, byte for byte. A
 * wordmark lockup cannot be a favicon: at 32px the type is noise. This crops
 * the glyph out and sets it on the brand's deep green.
 *
 * The crop was found by ink-column profiling, not by eye — the largest empty
 * column gap in the 1920x819 lockup falls at x643-669, which is the gutter
 * between glyph and wordmark.
 *
 * ## Why green and not the app's cream
 *
 * The glyph is a dark-green bowl with a mid-green leaf. On cream or white it
 * keeps almost no contrast by 32px. Rendered side by side at 32px, deep green
 * was the only one of cream / white / #004824 / #023c16 where the mark still
 * read as leaf-wheat-arrow.
 */
import sharp from 'sharp'
import { writeFileSync, mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..')

/** Glyph bounds inside the 1920x819 lockup. */
const CROP = { left: 74, top: 97, width: 570, height: 590 }
const GREEN = '#023c16' // --color-green-950

const glyph = (size) =>
  sharp(join(ROOT, 'public/brand/aniko-logo.png'))
    .extract(CROP)
    .resize(size, size, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toBuffer()

async function tile({ size = 512, radius = 115, inset = 0.7 } = {}) {
  const g = Math.round(size * inset)
  const bg = Buffer.from(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}">` +
      `<rect width="${size}" height="${size}" rx="${radius}" fill="${GREEN}"/></svg>`,
  )
  const offset = Math.round((size - g) / 2)
  return sharp(bg)
    .composite([{ input: await glyph(g), left: offset, top: offset }])
    .png({ compressionLevel: 9 })
    .toBuffer()
}

/** Render at full res then downsample, so the small icons stay sharp. */
const at = async (size, opts) =>
  sharp(await tile({ size: 512, ...opts }))
    .resize(size, size)
    .png({ compressionLevel: 9 })
    .toBuffer()

mkdirSync(join(ROOT, 'public/icons'), { recursive: true })
writeFileSync(join(ROOT, 'public/favicon.png'), await at(32))
writeFileSync(join(ROOT, 'public/icons/icon-192.png'), await at(192))
writeFileSync(join(ROOT, 'public/icons/icon-512.png'), await at(512))
// Full-bleed: iOS applies its own squircle mask to apple-touch-icon, so
// pre-rounded corners would composite their transparency to black.
writeFileSync(join(ROOT, 'public/icons/apple-touch-icon.png'), await at(180, { radius: 0 }))
// Maskable: 0.55 keeps the glyph's diagonal inside the centre-80% safe circle.
writeFileSync(
  join(ROOT, 'public/icons/icon-maskable-512.png'),
  await at(512, { radius: 0, inset: 0.55 }),
)
console.log('icons written to public/')
