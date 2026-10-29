import { http } from '@/api/http'
import type { PagedResult, ReportDto } from '@/api/types'

export const reportsApi = {
  list: (page: number) => http.get<PagedResult<ReportDto>>('/reports', { params: { page, pageSize: 10 } }).then((r) => r.data),
  request: (b: { serverId: number; startTime: string; endTime: string }) => http.post<ReportDto>('/reports', b).then((r) => r.data),
  async download(report: ReportDto) {
    // The endpoint needs the Authorization header, so fetch the file as a blob instead of a plain link.
    const res = await http.get<Blob>(`/reports/${report.id}/download`, { responseType: 'blob' })
    const url = URL.createObjectURL(res.data)
    const a = document.createElement('a')
    a.href = url
    a.download = `report-${report.id}.html`
    a.click()
    URL.revokeObjectURL(url)
  },
}