import { create } from 'zustand'
import type { AuthResponse, UserDto } from '@/api/types'
import { tokenStorage } from './tokenStorage'

type Status = 'initializing' | 'authenticated' | 'anonymous'

interface AuthState {
  user: UserDto | null
  accessToken: string | null          // memory only, never persisted
  accessTokenExpiresAt: number | null // epoch ms
  status: Status
  setSession: (auth: AuthResponse) => void
  clearSession: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  accessToken: null,
  accessTokenExpiresAt: null,
  status: 'initializing',

  setSession: (auth) => {
    tokenStorage.setRefreshToken(auth.refreshToken)
    set({
      user: auth.user,
      accessToken: auth.accessToken,
      accessTokenExpiresAt: Date.parse(auth.accessTokenExpiresAtUtc),
      status: 'authenticated',
    })
  },

  clearSession: () => {
    tokenStorage.clear()
    set({ user: null, accessToken: null, accessTokenExpiresAt: null, status: 'anonymous' })
  },
}))

export const useIsAdmin = () => useAuthStore((s) => s.user?.roles.includes('Admin') ?? false)