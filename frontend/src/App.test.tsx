import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { AppRoutes } from './App'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AppRoutes />
    </MemoryRouter>,
  )
}

test('the root redirects to the overview', () => {
  renderAt('/')
  expect(screen.getByText('Good morning, Juan Martinez')).toBeInTheDocument()
})

test('the overview greets the signed-in user and explains the page', () => {
  renderAt('/overview')
  expect(screen.getByText('Good morning, Juan Martinez')).toBeInTheDocument()
  expect(
    screen.getByText("Here's what's happening in your marketplace today."),
  ).toBeInTheDocument()
})

test('the overview renders the five panel slots phases C to G will fill', () => {
  const { container } = renderAt('/overview')
  for (const slot of ['stats', 'pricing', 'suppliers', 'lots', 'orders']) {
    expect(container.querySelector(`[data-slot="${slot}"]`)).not.toBeNull()
  }
})

test('unbuilt destinations render a named placeholder, not a dead link', () => {
  renderAt('/logistics')
  expect(screen.getByText('Logistics is not built yet.')).toBeInTheDocument()
  expect(screen.getByText('This section is planned. Nothing is broken.')).toBeInTheDocument()
})
