import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { DashboardShell } from './DashboardShell'

function renderShell() {
  return render(
    <MemoryRouter initialEntries={['/overview']}>
      <Routes>
        <Route element={<DashboardShell />}>
          <Route path="/overview" element={<p>panel slot</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

test('renders the sidebar, the header and the routed child', () => {
  renderShell()
  expect(screen.getByRole('complementary')).toBeInTheDocument()
  expect(screen.getByRole('banner')).toBeInTheDocument()
  expect(screen.getByText('panel slot')).toBeInTheDocument()
})

test('the content region is a responsive twelve-column grid', () => {
  renderShell()
  const main = screen.getByRole('main')
  expect(main).toHaveClass('grid')
  expect(main).toHaveClass('grid-cols-1')
  expect(main).toHaveClass('md:grid-cols-6')
  expect(main).toHaveClass('lg:grid-cols-12')
  expect(main).toHaveClass('gap-6')
})
