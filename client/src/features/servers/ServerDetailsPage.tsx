import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { getErrorMessage } from '@/api/errors'
import { Badge, statusTone } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ErrorMessage } from '@/components/ui/ErrorMessage'
import { Spinner } from '@/components/ui/Spinner'
import { StatusPage } from '@/components/StatusPage'
import { useIsAdmin } from '@/features/auth/authStore'
import { fmtDateTime } from '@/lib/format'
import { serversApi } from './api'
import { ServerForm } from './ServerForm'
import { ServerMetricsCharts } from './ServerMetricsCharts'

const ranges = { live: { label: 'Live (last 60)', hours: 0 }, '1h': { label: 'Last hour', hours: 1 }, '6h': { label: 'Last 6 hours', hours: 6 }, '24h': { label: 'Last 24 hours', hours: 24 } }
type RangeKey = keyof typeof ranges

export function ServerDetailsPage() {
  const id = Number(useParams().id)
  const navigate = useNavigate()
  const qc = useQueryClient()
  const isAdmin = useIsAdmin()
  const [editing, setEditing] = useState(false)
  const [range, setRange] = useState<RangeKey>('live')

  const from = useMemo(
    () => (ranges[range].hours ? new Date(Date.now() - ranges[range].hours * 3_600_000).toISOString() : undefined),
    [range],
  )

  const server = useQuery({
    queryKey: ['server', id],
    queryFn: () => serversApi.get(id),
    enabled: Number.isInteger(id),
    refetchInterval: 30_000,
  })

  const update = useMutation({
    mutationFn: (v: Parameters<typeof serversApi.update>[1]) => serversApi.update(id, v),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['server', id] })
      void qc.invalidateQueries({ queryKey: ['servers'] })
      setEditing(false)
    },
  })

  const remove = useMutation({
    mutationFn: () => serversApi.remove(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['servers'] })
      navigate('/dashboard')
    },
  })

  if (!Number.isInteger(id)) return <StatusPage code="404" title="Server not found" message="The address is not valid." />
  if (server.isPending) return <Spinner className="text-indigo-600" />
  if (server.isError) return <StatusPage code="404" title="Server not found" message={getErrorMessage(server.error)} />

  const s = server.data

  return (
    <div className="space-y-6">
      <Link to="/dashboard" className="text-sm text-indigo-600 hover:underline dark:text-indigo-400">Back to dashboard</Link>

      <Card>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="flex items-center gap-3 text-2xl font-semibold">{s.name} <Badge tone={statusTone(s.status)}>{s.status}</Badge></h1>
            <p className="text-sm text-slate-500">{s.ipAddress ?? 'no IP'} | added {fmtDateTime(s.createdAt)}</p>
            {s.description && <p className="mt-1 text-sm">{s.description}</p>}
          </div>
          {isAdmin && !editing && (
            <div className="flex gap-2">
              <Button variant="secondary" onClick={() => setEditing(true)}>Edit</Button>
              <Button variant="danger" loading={remove.isPending}
                onClick={() => { if (window.confirm(`Delete server "${s.name}"?`)) remove.mutate() }}>Delete</Button>
            </div>
          )}
        </div>
        {remove.isError && <div className="mt-3"><ErrorMessage message={getErrorMessage(remove.error)} /></div>}
        {editing && (
          <div className="mt-4">
            <ServerForm
              initial={{ name: s.name, ipAddress: s.ipAddress ?? '', description: s.description ?? '' }}
              submitLabel="Save changes"
              submitting={update.isPending}
              error={update.isError ? getErrorMessage(update.error) : null}
              onSubmit={(v) => update.mutate(v)}
              onCancel={() => setEditing(false)}
            />
          </div>
        )}
      </Card>

      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Metrics</h2>
        <label className="flex items-center gap-2 text-sm">
          Period
          <select value={range} onChange={(e) => setRange(e.target.value as RangeKey)}
            className="rounded-md border border-slate-300 bg-white px-2 py-1 dark:border-slate-700 dark:bg-slate-900">
            {Object.entries(ranges).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
          </select>
        </label>
      </div>
      <ServerMetricsCharts serverId={id} from={from} />
    </div>
  )
}