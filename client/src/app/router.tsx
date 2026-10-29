import { createBrowserRouter, Navigate } from 'react-router-dom'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { RequireAuth, RequireRole } from '@/features/auth/guards'
import { PlaceholderPage } from '@/components/PlaceholderPage'
import { StatusPage } from '@/components/StatusPage'
import { AppLayout } from './AppLayout'

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <Navigate to="/dashboard" replace /> },
          {
            path: 'dashboard',
            lazy: async () => ({ Component: (await import('@/features/dashboard/DashboardPage')).DashboardPage }),
          },
          { path: 'servers/:id', element: <PlaceholderPage title="Server details" /> },
          { path: 'alerts', element: <PlaceholderPage title="Alerts" /> },
          { path: 'reports', element: <PlaceholderPage title="Reports" /> },
          {
            element: <RequireRole role="Admin" />,
            children: [
              { path: 'jobs', element: <PlaceholderPage title="Background jobs" /> },
              { path: 'users', element: <PlaceholderPage title="Users" /> },
            ],
          },
          { path: '*', element: <StatusPage code="404" title="Page not found" message="This page does not exist." /> },
        ],
      },
    ],
  },
])