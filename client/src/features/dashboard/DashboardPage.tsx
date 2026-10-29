import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { http } from '@/api/http'
import type { PagedResult, ServerDto } from '@/api/types'
import { useHubEvent, useHubSubscription, useRealtime } from '@/realtime/realtimeContext'

export function DashboardPage() {
  const { state, onlineUsers } = useRealtime()
  const [received, setReceived] = useState(0)

  useHubSubscription('SubscribeToDashboard', 'UnsubscribeFromDashboard')
  useHubEvent('MetricReceived', () => setReceived((n) => n + 1))

  const servers = useQuery({
    queryKey: ['servers', 'smoke-test'],
    queryFn: () => http.get<PagedResult<ServerDto>>('/servers', { params: { pageSize: 100 } }).then((r) => r.data),
  })

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Dashboard (smoke test)</h1>
      <p>Connection: <b>{state}</b> | Live metrics received: <b>{received}</b></p>
      <p>Online: {onlineUsers.map((u) => `${u.userName} (${u.connections})`).join(', ') || 'nobody'}</p>
      {servers.isPending && <p>Loading servers…</p>}
      {servers.isError && <p role="alert">Could not load servers.</p>}
      <ul className="list-disc pl-6">
        {servers.data?.items.map((s) => <li key={s.id}>{s.name} - {s.status}</li>)}
      </ul>
    </div>
  )
}