import '@testing-library/jest-dom/vitest'

/*
 * Initialise i18n for every test. Without this, `t()` returns raw keys and any
 * assertion against visible copy fails for a reason that has nothing to do with
 * the component under test.
 */
import '@/lib/i18n'
