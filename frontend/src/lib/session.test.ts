import { renderHook } from '@testing-library/react'
import { useSession } from './session'

test('returns a loaded, verified buyer', () => {
  const { result } = renderHook(() => useSession())
  expect(result.current.isLoading).toBe(false)
  expect(result.current.user).not.toBeNull()
  expect(result.current.user?.role).toBe('buyer')
  expect(result.current.user?.verified).toBe(true)
  expect(result.current.user?.name).toBe('Juan Martinez')
})
