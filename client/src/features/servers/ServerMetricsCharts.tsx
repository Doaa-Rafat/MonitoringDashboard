import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { MetricDto } from '@/api/types'
import { Card } from '@/components/ui/Card'
import { Spinner } from '@/components/ui/Spinner'
import { fmtTime } from '@/lib/format'
import { useHubEvent, useHubSubscription } from '@/realtime/realtimeContext'
import { serversApi } from './api'

const LIVE_POINTS = 60
const RANGE_POINTS = 500

const charts = [
  { title: 'CPU', max: 100, series: [{ key: 'cpuUsage', name: 'CPU', unit: '%', color: '#6366f1' }] },
  { title: 'Memory', max: 100, series: [{ key: 'memoryUsage', name: 'Memory', unit: '%', color: '#10b981' }] },
  { title: 'Disk', max: 100, series: [{ key: 'diskUsage', name: 'Disk', unit: '%', color: '#f59e0b' }] },
  { title: 'Response time', max: undefined, series: [{ key: 'responseTime', name: 'Response time', unit: ' ms', color: '#ef4444' }] },
  {
    title: 'Network throughput', max: undefined, series: [
      { key: 'networkInKbps', name: 'Inbound', unit: ' Kbps', color: '#06b6d4' },
      { key: 'networkOutKbps', name: 'Outbound', unit: ' Kbps', color: '#8b5cf6' },
    ],
  },
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
        <Card key={c.title}>
          <h3 className="mb-2 text-sm font-medium">{c.title}</h3>
          {c.title === 'Network throughput' && data.every((m) => m.networkInKbps === null && m.networkOutKbps === null) ? (
            <p role="status" className="py-16 text-center text-sm text-slate-500">
              Network samples will appear after the next collection. Earlier network history is unavailable.
            </p>
          ) : (
            <div className="h-48" role="img" aria-label={`${c.title} over time`}>
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={data}>
                  <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                  <XAxis dataKey="timestamp" tickFormatter={(t: string) => fmtTime(t)} minTickGap={40} fontSize={11} />
                  <YAxis domain={[0, c.max ?? 'auto']} fontSize={11} width={40} />
                  <Tooltip
                    labelFormatter={(l) => fmtTime(String(l))}
                    formatter={(v, name) => {
                      const series = c.series.find((s) => s.name === name)
                      return [`${Number(v).toFixed(1)}${series?.unit ?? ''}`, String(name)]
                    }}
                  />
                  {c.series.length > 1 && <Legend />}
                  {c.series.map((s) => (
                    <Line key={s.key} type="monotone" dataKey={s.key} name={s.name} stroke={s.color}
                      dot={false} strokeWidth={2} isAnimationActive={false} />
                  ))}
                </LineChart>
              </ResponsiveContainer>
            </div>
          )}
        </Card>
      ))}
    </div>
  )
}
