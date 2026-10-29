import { http } from '@/api/http'
import type { AlertDto, AlertRuleDto, PagedResult } from '@/api/types'

export interface AlertFilter { status?: string; page?: number; pageSize?: number }

export const alertsApi = {
  list: (p: AlertFilter) => http.get<PagedResult<AlertDto>>('/alerts', { params: p }).then((r) => r.data),
  acknowledge: (id: number) => http.post<AlertDto>(`/alerts/${id}/acknowledge`).then((r) => r.data),
  resolve: (id: number) => http.post<AlertDto>(`/alerts/${id}/resolve`).then((r) => r.data),
  rules: () => http.get<AlertRuleDto[]>('/alert-rules').then((r) => r.data),
  updateRule: (id: number, b: { operator: string; threshold: number; isEnabled: boolean }) =>
    http.put<AlertRuleDto>(`/alert-rules/${id}`, b).then((r) => r.data),
}