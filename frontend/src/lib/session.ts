export type UserRole = 'buyer' | 'farmer'

export interface SessionUser {
  id: string
  name: string
  role: UserRole
  verified: boolean
  avatarUrl: string | null
}

export interface Session {
  user: SessionUser | null
  isLoading: boolean
}

const MOCK_USER: SessionUser = {
  id: 'usr_juan',
  name: 'Juan Martinez',
  role: 'buyer',
  verified: true,
  avatarUrl: null,
}

/**
 * Phase A returns a fixture synchronously. Phase I replaces the body with a
 * real fetch — the return shape must not change, because every consumer is
 * already written against `{ user, isLoading }`.
 */
export function useSession(): Session {
  return { user: MOCK_USER, isLoading: false }
}
