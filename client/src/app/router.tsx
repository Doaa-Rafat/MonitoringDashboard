import { createBrowserRouter, Navigate } from 'react-router-dom'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { RequireAuth, RequireRole } from '@/features/auth/guards'
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
          { path: 'servers/:id', lazy: async () => ({ Component: (await import('@/features/servers/ServerDetailsPage')).ServerDetailsPage }) },
          { path: 'alerts', lazy: async () => ({ Component: (await import('@/features/alerts/AlertsPage')).AlertsPage }) },
          { path: 'reports', lazy: async () => ({ Component: (await import('@/features/reports/ReportsPage')).ReportsPage }) },
          {
            element: <RequireRole role="Admin" />,
            children: [
              { path: 'jobs', lazy: async () => ({ Component: (await import('@/features/jobs/JobsPage')).JobsPage }) },
              { path: 'users', lazy: async () => ({ Component: (await import('@/features/users/UsersPage')).UsersPage }) },
            ],
          },
          { path: '*', element: <StatusPage code="404" title="Page not found" message="This page does not exist." /> },
        ],
      },
    ],
  },
])