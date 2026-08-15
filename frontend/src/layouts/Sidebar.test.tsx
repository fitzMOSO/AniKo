import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Sidebar } from './Sidebar'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Sidebar />
    </MemoryRouter>,
  )
}

test('renders all six destinations', () => {
  renderAt('/overview')
  for (const label of ['Overview', 'Marketplace', 'Orders', 'Messages', 'Logistics', 'Payments']) {
    expect(screen.getByRole('link', { name: new RegExp(label) })).toBeInTheDocument()
  }
})

test('marks the current destination as current', () => {
  renderAt('/overview')
  expect(screen.getByRole('link', { name: /Overview/ })).toHaveAttribute('aria-current', 'page')
  expect(screen.getByRole('link', { name: /Orders/ })).not.toHaveAttribute('aria-current')
})

test('messages carries an unread badge with an accessible label', () => {
  renderAt('/overview')
  expect(screen.getByLabelText('3 unread messages')).toHaveTextContent('3')
})

test('shows the signed-in user and their verified state', () => {
  renderAt('/overview')
  expect(screen.getByText('Juan Martinez')).toBeInTheDocument()
  expect(screen.getByText('Buyer')).toBeInTheDocument()
  expect(screen.getByText('Verified Account')).toBeInTheDocument()
})
