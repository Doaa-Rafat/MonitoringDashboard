import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { FullPageSpinner } from '@/components/ui/Spinner'
import { RealtimeProvider } from '@/realtime/RealtimeProvider'
import { StatusPage } from '@/components/StatusPage'
import { useAuthStore } from './authStore'

/** Everything inside requires a signed-in user. The SignalR connection lives only while signed in. */
export function RequireAuth() {
  const status = useAuthStore((s) => s.status)
  const location = useLocation()

  if (status === 'initializing') return <FullPageSpinner />
  if (status === 'anonymous') return <Navigate to="/login" replace state={{ from: location }} />

  return (
    <RealtimeProvider>
      <Outlet />
    </RealtimeProvider>
  )
}

export function RequireRole({ role }: { role: string }) {
  const allowed = useAuthStore((s) => s.user?.roles.includes(role) ?? false)
  if (!allowed) {
    return <StatusPage code="403" title="Access denied" message="You do not have permission to view this page." />
  }
  return <Outlet />
}