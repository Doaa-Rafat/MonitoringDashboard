import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { MetricDto } from '@/api/types'
import { Card } from '@/components/ui/Card'
import { Spinner } from '@/components/ui/Spinner'
import { fmtTime } from '@/lib/format'
import { useHubEvent, useHubSubscription } from '@/realtime/realtimeContext'
import { serversApi } from './api'

const LIVE_POINTS = 60
const RANGE_POINTS = 500

const charts = [
  { key: 'cpuUsage', title: 'CPU', unit: '%', color: '#6366f1', max: 100 },
  { key: 'memoryUsage', title: 'Memory', unit: '%', color: '#10b981', max: 100 },
  { key: 'diskUsage', title: 'Disk', unit: '%', color: '#f59e0b', max: 100 },
  { key: 'responseTime', title: 'Response time', unit: ' ms', color: '#ef4444', max: undefined },
] as const

/** History from the API, extended in real time by SignalR. `from` switches to a historical range. */
export function ServerMetricsCharts({ serverId, from }: { serverId: number; from?: string }) {
  const qc = useQueryClient()
  const limit = from ? RANGE_POINTS : LIVE_POINTS
  const queryKey = ['metrics', serverId, from ?? 'live']

  const { data, isPending, isError } = useQuery({
    queryKey,
    queryFn: async () => (await serversApi.metrics(serverId, { from, pageSize: limit })).items.slice().reverse(),
  })

  useHubSubscription('SubscribeToServer', 'UnsubscribeFromServer', serverId)
  useHubEvent('MetricReceived', (m) => {
    if (m.serverId !== serverId || from) return   // a historical range is not live
    qc.setQueryData<MetricDto[]>(queryKey, (old) => {
      // The same metric can arrive twice (server group and dashboard group), so ignore duplicates.
      if (!old || old.some((x) => x.id === m.id)) return old
      return [...old, m].slice(-limit)
    })
  })

  if (isPending) return <div className="flex justify-center py-10"><Spinner className="h-6 w-6 text-indigo-600" /></div>
  if (isError) return <p role="alert" className="text-sm text-red-600">Could not load metrics.</p>
  if (data.length === 0) return <p className="text-sm text-slate-500">No metrics in this period yet.</p>

  return (
    <div className="grid gap-4 md:grid-cols-2">
      {charts.map((c) => (
        <Card key={c.key}>
          <h3 className="mb-2 text-sm font-medium">{c.title}</h3>
          <div className="h-48" role="img" aria-label={`${c.title} over time`}>
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={data}>
                <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                <XAxis dataKey="timestamp" tickFormatter={(t: string) => fmtTime(t)} minTickGap={40} fontSize={11} />
                <YAxis domain={[0, c.max ?? 'auto']} fontSize={11} width={40} />
                <Tooltip
                  labelFormatter={(l) => fmtTime(String(l))}
                  formatter={(v) => [`${Number(v).toFixed(1)}${c.unit}`, c.title]}
                />
                <Line type="monotone" dataKey={c.key} stroke={c.color} dot={false} strokeWidth={2} isAnimationActive={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </Card>
      ))}
    </div>
  )
}