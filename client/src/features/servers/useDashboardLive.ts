import { useQueryClient } from '@tanstack/react-query'
import type { PagedResult, ServerDto } from '@/api/types'
import { useHubEvent, useHubSubscription } from '@/realtime/realtimeContext'

/** Keeps the cached server list (status and latest metric) in sync with live metrics. */
export function useDashboardLive() {
  const qc = useQueryClient()
  useHubSubscription('SubscribeToDashboard', 'UnsubscribeFromDashboard')
  useHubEvent('MetricReceived', (m) => {
    qc.setQueryData<PagedResult<ServerDto>>(['servers'], (old) =>
      old && {
        ...old,
        items: old.items.map((s) =>
          s.id === m.serverId
            ? {
                ...s,
                status: m.status,
                latestMetric: {
                  cpuUsage: m.cpuUsage, memoryUsage: m.memoryUsage, diskUsage: m.diskUsage,
                  responseTime: m.responseTime, networkInKbps: m.networkInKbps,
                  networkOutKbps: m.networkOutKbps, timestamp: m.timestamp,
                },
              }
            : s,
        ),
      },
    )
  })
}
