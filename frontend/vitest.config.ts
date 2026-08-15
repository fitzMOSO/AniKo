import { defineConfig, mergeConfig } from 'vitest/config'
import viteConfig from './vite.config.ts'

/*
 * Merged with the app's vite config on purpose. Vitest does NOT inherit
 * vite.config.ts automatically when a vitest.config.ts exists, so without this
 * the `@/` alias resolves in the app and fails in tests.
 */
export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/test/setup.ts'],
    },
  }),
)
