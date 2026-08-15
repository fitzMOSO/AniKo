import { describe, expect, it } from 'vitest'

/**
 * Reads the source tree through Vite rather than `node:fs`.
 *
 * The app tsconfig deliberately limits `types` to `vite/client` and
 * `vitest/globals` — Node's globals are kept out so a component cannot import
 * `fs` and still typecheck. Pulling in `@types/node` for this one guard would
 * weaken that boundary for every file in `src`. `import.meta.glob` gets the
 * same file contents without it.
 */
const SOURCES = import.meta.glob('/src/**/*.{ts,tsx}', {
  query: '?raw',
  import: 'default',
  eager: true,
}) as Record<string, string>

/** Colour may be literal only in the files whose job is defining it. */
const ALLOWED = new Set(['chart-theme.ts', 'color.ts', 'color.test.ts', 'palette.test.ts'])

function offenders(): string[] {
  return Object.entries(SOURCES)
    .filter(([path]) => !ALLOWED.has(path.split('/').pop() ?? ''))
    .flatMap(([path, source]) =>
      (source.match(/#[0-9a-fA-F]{6}\b/g) ?? []).map((hit) => `${path}: ${hit}`),
    )
}

describe('colour discipline', () => {
  it('keeps literal hex out of components', () => {
    expect(offenders(), 'use a token or chart-theme.ts instead of a literal colour').toEqual([])
  })

  it('actually scans the tree, rather than passing on an empty file list', () => {
    // Without this, a glob that silently matched nothing would make the guard
    // above vacuously true, and every future hardcoded colour would sail past.
    expect(Object.keys(SOURCES).length).toBeGreaterThan(10)
  })
})
