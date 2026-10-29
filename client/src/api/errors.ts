import axios from 'axios'
import type { ProblemDetails } from './types'

export function getErrorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (axios.isAxiosError<ProblemDetails>(error)) {
    if (!error.response) return 'Cannot reach the server. Check your connection and try again.'
    if (error.response.status === 429) return 'Too many attempts. Please wait a minute and try again.'

    const problem = error.response.data
    const firstFieldError = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined
    if (firstFieldError) return firstFieldError
    if (problem?.detail) return problem.detail
    if (problem?.title) return problem.title
  }
  return fallback
}