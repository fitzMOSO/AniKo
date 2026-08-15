import '@testing-library/jest-dom/vitest'

/*
 * Initialise i18n for every test. Without this, `t()` returns raw keys and any
 * assertion against visible copy fails for a reason that has nothing to do with
 * the component under test.
 */
import '@/lib/i18n'

/*
 * DO NOT add a `ResizeObserver` polyfill or mock here.
 *
 * jsdom has none, and Recharts 3 reads that absence as "skip measuring, keep
 * the seed dimension" — which is the only reason charts render in these tests
 * at all. Adding the usual no-op mock re-enables the measuring effect, which
 * then reads 0x0 from `getBoundingClientRect` and overwrites the seed. The
 * chart renders empty, nothing throws, and no warning points back at this file.
 *
 * Measured against recharts@3.10.1 + jsdom@30. See
 * docs/superpowers/plans/2026-08-16-aniko-dashboard-phase-d.md.
 */
