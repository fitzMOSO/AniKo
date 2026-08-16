import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AppRoutes } from '@/App'
import { NAV_ITEMS } from '@/app/nav'

/**
 * One cross-cutting checklist item, enforced once here instead of restated in
 * every panel's own suite: every interactive element has an accessible name.
 *
 * This is a whole-app property. A panel test can only assert the names it
 * knows to look for, so an icon-only button added later — the exact shape that
 * loses its name — passes every existing test. Walking the rendered routes
 * catches it wherever it lands.
 *
 * The name is computed by Testing Library's own `name` option, which runs the
 * real accessible-name algorithm. Comparing "elements with this role" against
 * "elements with this role and a non-whitespace name" means the check uses only
 * the public API — no direct dependency on `dom-accessibility-api`, which this
 * project gets transitively and does not declare.
 */
const INTERACTIVE = [
  'button',
  'link',
  'textbox',
  'searchbox',
  'combobox',
  'checkbox',
  'switch',
] as const

/** Anything carrying a role above but no name it could be announced by. */
function unnamed(): string[] {
  return INTERACTIVE.flatMap((role) => {
    const all = screen.queryAllByRole(role)
    if (all.length === 0) return []
    const named = new Set(screen.queryAllByRole(role, { name: /\S/ }))
    return all.filter((el) => !named.has(el)).map((el) => `${role}: ${el.outerHTML.slice(0, 120)}`)
  })
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AppRoutes />
    </MemoryRouter>,
  )
}

describe('accessible names', () => {
  /*
   * The overview is the only built route, and it carries the densest set of
   * controls in the app: the header's icon-only buttons, the range select, six
   * bookmark toggles, six quote triggers, and a profile link per supplier row.
   */
  it('names every control on the overview', async () => {
    renderAt('/overview')

    expect(unnamed()).toEqual([])

    /*
     * Again once the lazy map has resolved: its pins are buttons whose only
     * name is a visually hidden span, which is precisely the fragile case.
     *
     * The timeout is raised from the default second because this test renders
     * the entire app — two lazy chunks, not one — and the whole suite runs in
     * parallel. It failed here at the default while passing in isolation, which
     * is a property of the harness rather than of the map; the fix is to wait
     * long enough, not to stop asserting on the pins.
     */
    await screen.findByRole('group', { name: /map of nearby/i }, { timeout: 5000 })
    expect(unnamed()).toEqual([])
  })

  /*
   * The placeholder routes are mostly chrome, but that chrome is the shell —
   * sidebar, header, mobile nav — and a name lost there is lost on every page
   * at once.
   */
  it.each(NAV_ITEMS.filter((item) => item.to !== '/overview').map((item) => item.to))(
    'names every control on %s',
    (path) => {
      renderAt(path)
      expect(unnamed()).toEqual([])
    },
  )
})
