import { http } from '@/api/http'
import type { AuthResponse } from '@/api/types'

export interface LoginInput { userNameOrEmail: string; password: string }
export interface RegisterInput { userName: string; email: string; password: string }

export const authApi = {
  login: (body: LoginInput) => http.post<AuthResponse>('/auth/login', body).then((r) => r.data),
  register: (body: RegisterInput) => http.post<AuthResponse>('/auth/register', body).then((r) => r.data),
  logout: () => http.post('/auth/logout'),
}