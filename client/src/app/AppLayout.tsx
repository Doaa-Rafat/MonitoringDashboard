import { NavLink, Outlet } from 'react-router-dom'
import { authApi } from '@/features/auth/api'
import { useAuthStore, useIsAdmin } from '@/features/auth/authStore'
import { useThemeStore } from '@/features/theme/themeStore'
import { Button } from '@/components/ui/Button'
import { useRealtime, type ConnectionState } from '@/realtime/realtimeContext'
import { cn } from '@/lib/cn'
import { queryClient } from './queryClient'
import {AlertToasts} from '@/features/alerts/AlertToasts' 
import {useLiveInvalidation} from '@/realtime/useLiveInvalidation'  

const connectionLabel: Record<ConnectionState, { text: string; dot: string }> = {
  connected: { text: 'Live', dot: 'bg-emerald-500' },
  connecting: { text: 'Connecting…', dot: 'bg-amber-500' },
  reconnecting: { text: 'Reconnecting…', dot: 'bg-amber-500' },
  disconnected: { text: 'Offline', dot: 'bg-slate-400' },
}

function ConnectionBadge() {
  const { state } = useRealtime()
  const { text, dot } = connectionLabel[state]
  return (
    <span className="inline-flex items-center gap-2 text-xs" role="status" aria-live="polite">
      <span className={cn('h-2 w-2 rounded-full', dot)} aria-hidden="true" />
      {text}
    </span>
  )
}

export function AppLayout() {
  const user = useAuthStore((s) => s.user)
  const clearSession = useAuthStore((s) => s.clearSession)
  const isAdmin = useIsAdmin()
  const { theme, toggle } = useThemeStore()
  useLiveInvalidation()
  const links = [
    { to: '/dashboard', label: 'Dashboard' },
    { to: '/alerts', label: 'Alerts' },
    { to: '/reports', label: 'Reports' },
    ...(isAdmin ? [{ to: '/jobs', label: 'Jobs' }, { to: '/users', label: 'Users' }] : []),
  ]

  const signOut = async () => {
    try { await authApi.logout() } catch { /* the local session is cleared either way */ }
    queryClient.clear()
    clearSession() // RequireAuth redirects to /login and the SignalR connection closes
  }

  return (
    <div className="min-h-screen">
      <AlertToasts />
      <header className="border-b border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-3">
          <span className="font-semibold">Monitoring</span>

          <nav aria-label="Main" className="order-3 -mx-1 flex w-full gap-1 overflow-x-auto sm:order-none sm:mx-0 sm:w-auto">
            {links.map((l) => (
              <NavLink
                key={l.to}
                to={l.to}
                className={({ isActive }) =>
                  cn(
                    'whitespace-nowrap rounded-md px-3 py-1.5 text-sm font-medium',
                    isActive
                      ? 'bg-indigo-50 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300'
                      : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
                  )
                }
              >
                {l.label}
              </NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-3">
            <ConnectionBadge />
            <button
              type="button"
              onClick={toggle}
              aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} theme`}
              className="rounded-md px-2 py-1 text-sm hover:bg-slate-100 dark:hover:bg-slate-800"
            >
              {theme === 'dark' ? 'Light' : 'Dark'}
            </button>
            <span className="hidden text-sm sm:inline">{user?.userName}</span>
            <Button variant="secondary" onClick={signOut}>Sign out</Button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}