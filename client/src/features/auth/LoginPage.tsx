import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { getErrorMessage } from '@/api/errors'
import { Button } from '@/components/ui/Button'
import { ErrorMessage } from '@/components/ui/ErrorMessage'
import { TextField } from '@/components/ui/TextField'
import { authApi } from './api'
import { useAuthStore } from './authStore'

const schema = z.object({
  userNameOrEmail: z.string().trim().min(1, 'Enter your username or email'),
  password: z.string().min(1, 'Enter your password'),
})
type FormValues = z.infer<typeof schema>

export function LoginPage() {
  const status = useAuthStore((s) => s.status)
  const setSession = useAuthStore((s) => s.setSession)
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/dashboard'

  const { register, handleSubmit, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
  })

  const login = useMutation({
    mutationFn: authApi.login,
    onSuccess: (auth) => {
      setSession(auth)
      navigate(from, { replace: true })
    },
  })

  if (status === 'authenticated') return <Navigate to="/dashboard" replace />

  return (
    <main className="flex min-h-screen items-center justify-center px-4">
      <form
        onSubmit={handleSubmit((values) => login.mutate(values))}
        noValidate
        className="w-full max-w-sm space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900"
      >
        <h1 className="text-xl font-semibold">Sign in</h1>

        {login.isError && <ErrorMessage message={getErrorMessage(login.error)} />}

        <TextField label="Username or email" autoComplete="username" error={errors.userNameOrEmail?.message} {...register('userNameOrEmail')} />
        <TextField label="Password" type="password" autoComplete="current-password" error={errors.password?.message} {...register('password')} />

        <Button type="submit" loading={login.isPending} className="w-full">Sign in</Button>

        <p className="text-center text-sm text-slate-600 dark:text-slate-400">
          No account? <Link to="/register" className="text-indigo-600 hover:underline">Create one</Link>
        </p>
      </form>
    </main>
  )
}