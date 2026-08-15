import { render, screen } from '@testing-library/react'
import App from './App'

test('renders the app name using the brand colour token', () => {
  render(<App />)
  const heading = screen.getByText('AniKo')
  expect(heading).toBeInTheDocument()
  expect(heading).toHaveClass('text-primary')
})
