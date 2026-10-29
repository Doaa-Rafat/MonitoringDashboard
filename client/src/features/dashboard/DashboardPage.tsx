import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { getErrorMessage } from '@/api/errors'
import { Badge, statusTone } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Spinner } from '@/components/ui/Spinner'
import { alertsApi } from '@/features/alerts/api'
import { useIsAdmin } from '@/features/auth/authStore'
import { serversApi } from '@/features/servers/api'
import { ServerForm } from '@/features/servers/ServerForm'
import { ServerMetricsCharts } from '@/features/servers/ServerMetricsCharts'
import { useDashboardLive } from '@/features/servers/useDashboardLive'
import { cn } from '@/lib/cn'
import { fmtDateTime, fmtNum } from '@/lib/format'
import { useRealtime } from '@/realtime/realtimeContext'

export function DashboardPage() {
  const qc = useQueryClient()
  const isAdmin = useIsAdmin()
  const { onlineUsers } = useRealtime()
  const [selected, setSelected] = useState<number | null>(null)
  const [showForm, setShowForm] = useState(false)

  useDashboardLive()

  const servers = useQuery({ queryKey: ['servers'], queryFn: serversApi.list })
  const openAlerts = useQuery({
    queryKey: ['alerts', 'open'],
    queryFn: () => alertsApi.list({ status: 'Triggered', pageSize: 5 }),
  })

  const create = useMutation({
    mutationFn: serversApi.create,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['servers'] })
      setShowForm(false)
    },
  })

  const items = servers.data?.items ?? []
  const selectedId = selected ?? items[0]?.id ?? null
  const up = items.filter((s) => s.status === 'Up').length

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl font-semibold">Dashboard</h1>
        {isAdmin && !showForm && <Button onClick={() => setShowForm(true)}>Add server</Button>}
      </div>

      {showForm && (
        <Card>
          <ServerForm
            submitLabel="Add server"
            submitting={create.isPending}
            error={create.isError ? getErrorMessage(create.error) : null}
            onSubmit={(v) => create.mutate(v)}
            onCancel={() => setShowForm(false)}
          />
        </Card>
      )}

      <section aria-label="Summary" className="grid gap-4 sm:grid-cols-3">
        <Card><p className="text-sm text-slate-500">Servers up</p><p className="text-2xl font-semibold">{up} / {items.length}</p></Card>
        <Card><p className="text-sm text-slate-500">Open alerts</p><p className="text-2xl font-semibold">{openAlerts.data?.totalCount ?? '-'}</p></Card>
        <Card>
          <p className="text-sm text-slate-500">Online now</p>
          <p className="truncate text-sm font-medium">{onlineUsers.map((u) => u.userName).join(', ') || 'nobody'}</p>
        </Card>
      </section>

      <section aria-label="Servers">
        <h2 className="mb-3 text-lg font-semibold">Servers</h2>
        {servers.isPending && <Spinner className="text-indigo-600" />}
        {servers.isError && <p role="alert" className="text-red-600">{getErrorMessage(servers.error)}</p>}
        {servers.isSuccess && items.length === 0 && <p className="text-slate-500">No servers yet.</p>}
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((s) => (
            <Card key={s.id} className={cn(s.id === selectedId && 'ring-2 ring-indigo-500')}>
              <div className="flex items-start justify-between gap-2">
                <div>
                  <Link to={`/servers/${s.id}`} className="font-medium text-indigo-600 hover:underline dark:text-indigo-400">{s.name}</Link>
                  <p className="text-xs text-slate-500">{s.ipAddress ?? 'no IP'}</p>
                </div>
                <Badge tone={statusTone(s.status)}>{s.status}</Badge>
              </div>
              {s.latestMetric ? (
                <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
                  <dt className="text-slate-500">CPU</dt><dd>{fmtNum(s.latestMetric.cpuUsage, '%')}</dd>
                  <dt className="text-slate-500">Memory</dt><dd>{fmtNum(s.latestMetric.memoryUsage, '%')}</dd>
                  <dt className="text-slate-500">Disk</dt><dd>{fmtNum(s.latestMetric.diskUsage, '%')}</dd>
                  <dt className="text-slate-500">Response</dt><dd>{fmtNum(s.latestMetric.responseTime, ' ms')}</dd>
                  <dt className="text-slate-500">Network in</dt><dd>{s.latestMetric.networkInKbps === null ? '—' : fmtNum(s.latestMetric.networkInKbps, ' Kbps')}</dd>
                  <dt className="text-slate-500">Network out</dt><dd>{s.latestMetric.networkOutKbps === null ? '—' : fmtNum(s.latestMetric.networkOutKbps, ' Kbps')}</dd>
                </dl>
              ) : (
                <p className="mt-3 text-sm text-slate-500">No metrics yet.</p>
              )}
              <button type="button" onClick={() => setSelected(s.id)} className="mt-3 text-sm text-indigo-600 hover:underline dark:text-indigo-400">
                Show live charts
              </button>
            </Card>
          ))}
        </div>
      </section>

      {selectedId !== null && (
        <section aria-label="Live charts">
          <h2 className="mb-3 text-lg font-semibold">Live charts: {items.find((s) => s.id === selectedId)?.name}</h2>
          <ServerMetricsCharts serverId={selectedId} />
        </section>
      )}

      <section aria-label="Latest alerts">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-semibold">Latest open alerts</h2>
          <Link to="/alerts" className="text-sm text-indigo-600 hover:underline dark:text-indigo-400">All alerts</Link>
        </div>
        {openAlerts.data?.items.length === 0 && <p className="text-slate-500">No open alerts.</p>}
        <ul className="space-y-2">
          {openAlerts.data?.items.map((a) => (
            <li key={a.id} className="rounded-md border border-slate-200 px-3 py-2 text-sm dark:border-slate-800">
              <b>{a.serverName}</b>: {a.metricType} {fmtNum(a.metricValue)} (threshold {fmtNum(a.threshold)})
              <span className="ml-2 text-slate-500">{fmtDateTime(a.createdAt)}</span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
