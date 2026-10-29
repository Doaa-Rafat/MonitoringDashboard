import axios from 'axios'
import type { AuthResponse } from '@/api/types'
import { useAuthStore } from './authStore'
import { tokenStorage } from './tokenStorage'

let inflight: Promise<AuthResponse> | null = null

/**
 * Exchanges the stored refresh token for a new session.
 * Concurrent callers share ONE request: refresh tokens rotate, so two parallel
 * refreshes would invalidate each other and log the user out.
 */
export function refreshSession(): Promise<AuthResponse> {
  if (inflight) return inflight

  const refreshToken = tokenStorage.getRefreshToken()
  if (!refreshToken) return Promise.reject(new Error('No refresh token'))

  // A bare axios call (not the intercepted instance) so a failing refresh cannot loop.
  inflight = axios
    .post<AuthResponse>('/api/v1/auth/refresh', { refreshToken })
    .then((res) => {
      useAuthStore.getState().setSession(res.data)
      return res.data
    })
    .catch((err: unknown) => {
      // Only a rejected token ends the session; a network error keeps it for a later retry.
      if (axios.isAxiosError(err) && err.response?.status === 401) {
        useAuthStore.getState().clearSession()
      }
      throw err
    })
    .finally(() => {
      inflight = null
    })

  return inflight
}


export async function initializeSession(): Promise<void> {
  if (!tokenStorage.getRefreshToken()) {
    useAuthStore.getState().clearSession()
    return
  }
  try {
    await refreshSession()
  } catch {
    if (useAuthStore.getState().status === 'initializing') {
      useAuthStore.setState({ status: 'anonymous' }) 
    }
  }
}

/** For SignalR */
export async function getValidAccessToken(): Promise<string> {
  const { accessToken, accessTokenExpiresAt } = useAuthStore.getState()
  if (accessToken && accessTokenExpiresAt !== null && accessTokenExpiresAt - Date.now() > 30_000) {
    return accessToken
  }
  const session = await refreshSession()
  return session.accessToken
}