export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

export interface ProblemDetails {
  status?: number
  title?: string
  detail?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export interface UserDto { id: number; userName: string; email: string; roles: string[] }
export interface AuthResponse {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAtUtc: string
  user: UserDto
}

export interface LatestMetricDto {
  cpuUsage: number; memoryUsage: number; diskUsage: number; responseTime: number
  networkInKbps: number | null; networkOutKbps: number | null; timestamp: string
}
export interface ServerDto {
  id: number; name: string; ipAddress: string | null; status: 'Up' | 'Down'
  description: string | null; createdAt: string; latestMetric: LatestMetricDto | null
}
export interface MetricDto {
  id: number; serverId: number; cpuUsage: number; memoryUsage: number; diskUsage: number
  responseTime: number; status: 'Up' | 'Down'; timestamp: string
  networkInKbps: number | null; networkOutKbps: number | null
}

export type AlertStatus = 'Triggered' | 'Acknowledged' | 'Resolved'
export interface AlertDto {
  id: number; serverId: number; serverName: string; alertRuleId: number | null
  metricType: string; metricValue: number; threshold: number
  status: AlertStatus; createdAt: string; resolvedAt: string | null
}
export interface AlertRuleDto {
  id: number; serverId: number | null; serverName: string | null; metricType: string
  operator: string; threshold: number; isEnabled: boolean; createdAt: string
}

export type ReportStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed'
export interface ReportDto {
  id: number; serverId: number; serverName: string; startTime: string; endTime: string
  status: ReportStatus; createdAt: string; completedAt: string | null; isDownloadable: boolean
}

export interface OnlineUserDto { userId: number; userName: string; connections: number }
export interface JobDto { id: string; name: string; state: string; timestamp: string | null; durationMs: number | null; error: string | null }
export interface RecurringJobDto { id: string; cron: string; nextExecution: string | null; lastExecution: string | null; lastJobState: string | null; error: string | null }
export interface JobCountsDto { enqueued: number; scheduled: number; processing: number; succeeded: number; failed: number; deleted: number; recurring: number; servers: number }
export interface ManagedUserDto { id: number; userName: string; email: string; roles: string[]; createdAt: string }
