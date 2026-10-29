import { http } from '@/api/http'
import type { MetricDto, PagedResult, ServerDto } from '@/api/types'

export interface ServerInput { name: string; ipAddress?: string; description?: string }

export const serversApi = {
  list: () => http.get<PagedResult<ServerDto>>('/servers', { params: { pageSize: 100, sortBy: 'name' } }).then((r) => r.data),
  get: (id: number) => http.get<ServerDto>(`/servers/${id}`).then((r) => r.data),
  create: (b: ServerInput) => http.post<ServerDto>('/servers', b).then((r) => r.data),
  update: (id: number, b: ServerInput) => http.put<ServerDto>(`/servers/${id}`, b).then((r) => r.data),
  remove: (id: number) => http.delete(`/servers/${id}`),
  metrics: (id: number, p: { from?: string; pageSize: number }) =>
    http.get<PagedResult<MetricDto>>(`/servers/${id}/metrics`, { params: { ...p, sortDirection: 'desc' } }).then((r) => r.data),
}