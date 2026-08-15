import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Header } from './Header'

test('renders search, the mode toggle, the CTA and the account controls', () => {
  render(<Header />)
  expect(screen.getByRole('searchbox', { name: /search crops/i })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Buy' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Sell' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'List Produce' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Notifications' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Your account' })).toBeInTheDocument()
})

test('buy is the selected mode by default', () => {
  render(<Header />)
  expect(screen.getByRole('button', { name: 'Buy' })).toHaveAttribute('aria-pressed', 'true')
  expect(screen.getByRole('button', { name: 'Sell' })).toHaveAttribute('aria-pressed', 'false')
})

test('selecting sell moves the pressed state', async () => {
  render(<Header />)
  await userEvent.click(screen.getByRole('button', { name: 'Sell' }))
  expect(screen.getByRole('button', { name: 'Sell' })).toHaveAttribute('aria-pressed', 'true')
  expect(screen.getByRole('button', { name: 'Buy' })).toHaveAttribute('aria-pressed', 'false')
})
