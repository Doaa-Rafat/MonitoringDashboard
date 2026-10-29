import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { getErrorMessage } from '@/api/errors'
import { Button } from '@/components/ui/Button'
import { ErrorMessage } from '@/components/ui/ErrorMessage'
import { TextField } from '@/components/ui/TextField'
import { authApi } from './api'
import { useAuthStore } from './authStore'

// Mirrors the API's RegisterCommandValidator so users get instant feedback; the API stays the authority.
const schema = z
  .object({
    userName: z
      .string().trim()
      .min(3, 'At least 3 characters')
      .max(50, 'At most 50 characters')
      .regex(/^[a-zA-Z0-9_.-]+$/, "Only letters, digits, '_', '.' and '-' are allowed"),
    email: z.string().trim().max(100).email('Enter a valid email address'),
    password: z
      .string()
      .min(8, 'At least 8 characters')
      .regex(/[A-Z]/, 'Must contain an uppercase letter')
      .regex(/[a-z]/, 'Must contain a lowercase letter')
      .regex(/[0-9]/, 'Must contain a digit'),
    confirmPassword: z.string(),
  })
  .refine((v) => v.password === v.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  })
type FormValues = z.infer<typeof schema>

export function RegisterPage() {
  const status = useAuthStore((s) => s.status)
  const setSession = useAuthStore((s) => s.setSession)
  const navigate = useNavigate()

  const { register, handleSubmit, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
  })

  const signUp = useMutation({
    mutationFn: authApi.register,
    onSuccess: (auth) => {
      setSession(auth)
      navigate('/dashboard', { replace: true })
    },
  })

  if (status === 'authenticated') return <Navigate to="/dashboard" replace />

  return (
    <main className="flex min-h-screen items-center justify-center px-4">
      <form
        onSubmit={handleSubmit(({ userName, email, password }) => signUp.mutate({ userName, email, password }))}
        noValidate
        className="w-full max-w-sm space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900"
      >
        <h1 className="text-xl font-semibold">Create an account</h1>

        {signUp.isError && <ErrorMessage message={getErrorMessage(signUp.error)} />}

        <TextField label="Username" autoComplete="username" error={errors.userName?.message} {...register('userName')} />
        <TextField label="Email" type="email" autoComplete="email" error={errors.email?.message} {...register('email')} />
        <TextField label="Password" type="password" autoComplete="new-password" error={errors.password?.message} {...register('password')} />
        <TextField label="Confirm password" type="password" autoComplete="new-password" error={errors.confirmPassword?.message} {...register('confirmPassword')} />

        <Button type="submit" loading={signUp.isPending} className="w-full">Create account</Button>

        <p className="text-center text-sm text-slate-600 dark:text-slate-400">
          Already registered? <Link to="/login" className="text-indigo-600 hover:underline">Sign in</Link>
        </p>
      </form>
    </main>
  )
}