import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { MobileNav } from './MobileNav'

function renderNav() {
  return render(
    <MemoryRouter>
      <MobileNav />
    </MemoryRouter>,
  )
}

describe('MobileNav', () => {
  it('exposes a labelled trigger', () => {
    renderNav()
    expect(screen.getByRole('button', { name: /open navigation menu/i })).toBeInTheDocument()
  })

  it('reveals the navigation destinations once opened', async () => {
    const user = userEvent.setup()
    renderNav()

    expect(screen.queryByRole('link', { name: /marketplace/i })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /open navigation menu/i }))

    expect(await screen.findByRole('link', { name: /marketplace/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /payments/i })).toBeInTheDocument()
  })

  it('closes again, so the drawer cannot strand the user', async () => {
    const user = userEvent.setup()
    renderNav()

    await user.click(screen.getByRole('button', { name: /open navigation menu/i }))
    await screen.findByRole('link', { name: /marketplace/i })

    await user.click(screen.getByRole('button', { name: /close navigation menu/i }))

    expect(screen.queryByRole('link', { name: /marketplace/i })).not.toBeInTheDocument()
  })
})
