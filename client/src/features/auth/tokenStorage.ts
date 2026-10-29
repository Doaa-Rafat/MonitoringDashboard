const KEY = 'md.refreshToken'

export const tokenStorage = {
  getRefreshToken(): string | null {
    try { return localStorage.getItem(KEY) } catch { return null }
  },
  setRefreshToken(token: string): void {
    try { localStorage.setItem(KEY, token) } catch { /* storage unavailable: the session will not survive a reload */ }
  },
  clear(): void {
    try { localStorage.removeItem(KEY) } catch { /* ignore */ }
  },
}