import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { getErrorMessage } from '@/api/errors'
import { Badge, statusTone } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ErrorMessage } from '@/components/ui/ErrorMessage'
import { Spinner } from '@/components/ui/Spinner'
import { useIsAdmin } from '@/features/auth/authStore'
import { fmtDateTime, fmtNum } from '@/lib/format'
import { alertsApi } from './api'

const PAGE_SIZE = 10

export function AlertsPage() {
  const qc = useQueryClient()
  const isAdmin = useIsAdmin()
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)

  const alerts = useQuery({
    queryKey: ['alerts', 'list', status, page],
    queryFn: () => alertsApi.list({ status: status || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  })

  const refresh = () => void qc.invalidateQueries({ queryKey: ['alerts'] })
  const ack = useMutation({ mutationFn: alertsApi.acknowledge, onSuccess: refresh })
  const resolve = useMutation({ mutationFn: alertsApi.resolve, onSuccess: refresh })
  const actionError = ack.error ?? resolve.error

  const rules = useQuery({ queryKey: ['alerts', 'rules'], queryFn: alertsApi.rules })
  const updateRule = useMutation({ mutationFn: (v: { id: number; threshold: number; isEnabled: boolean; operator: string }) =>
    alertsApi.updateRule(v.id, { operator: v.operator, threshold: v.threshold, isEnabled: v.isEnabled }), onSuccess: refresh })

  const data = alerts.data

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Alerts</h1>
        <label className="flex items-center gap-2 text-sm">
          Status
          <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}
            className="rounded-md border border-slate-300 bg-white px-2 py-1 dark:border-slate-700 dark:bg-slate-900">
            <option value="">All</option>
            <option>Triggered</option>
            <option>Acknowledged</option>
            <option>Resolved</option>
          </select>
        </label>
      </div>

      {actionError && <ErrorMessage message={getErrorMessage(actionError)} />}
      {alerts.isPending && <Spinner className="text-indigo-600" />}
      {alerts.isError && <ErrorMessage message={getErrorMessage(alerts.error)} />}

      {data && (
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead className="border-b border-slate-200 text-slate-500 dark:border-slate-800">
              <tr><th className="p-3">Time</th><th className="p-3">Server</th><th className="p-3">Metric</th><th className="p-3">Value</th><th className="p-3">Status</th><th className="p-3">Actions</th></tr>
            </thead>
            <tbody>
              {data.items.length === 0 && <tr><td colSpan={6} className="p-4 text-slate-500">No alerts.</td></tr>}
              {data.items.map((a) => (
                <tr key={a.id} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                  <td className="p-3">{fmtDateTime(a.createdAt)}</td>
                  <td className="p-3"><Link to={`/servers/${a.serverId}`} className="text-indigo-600 hover:underline dark:text-indigo-400">{a.serverName}</Link></td>
                  <td className="p-3">{a.metricType}</td>
                  <td className="p-3">{fmtNum(a.metricValue)} / {fmtNum(a.threshold)}</td>
                  <td className="p-3"><Badge tone={statusTone(a.status)}>{a.status}</Badge></td>
                  <td className="space-x-2 p-3 whitespace-nowrap">
                    {a.status === 'Triggered' && <Button variant="secondary" onClick={() => ack.mutate(a.id)}>Acknowledge</Button>}
                    {a.status !== 'Resolved' && <Button variant="secondary" onClick={() => resolve.mutate(a.id)}>Resolve</Button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="flex items-center justify-between p-3 text-sm">
            <span>Page {data.page} of {Math.max(data.totalPages, 1)} ({data.totalCount} alerts)</span>
            <span className="space-x-2">
              <Button variant="secondary" disabled={!data.hasPrevious} onClick={() => setPage((p) => p - 1)}>Previous</Button>
              <Button variant="secondary" disabled={!data.hasNext} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </span>
          </div>
        </Card>
      )}

      <section aria-label="Alert rules">
        <h2 className="mb-3 text-lg font-semibold">Threshold rules</h2>
        {updateRule.isError && <ErrorMessage message={getErrorMessage(updateRule.error)} />}
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[560px] text-left text-sm">
            <thead className="border-b border-slate-200 text-slate-500 dark:border-slate-800">
              <tr><th className="p-3">Scope</th><th className="p-3">Metric</th><th className="p-3">Condition</th><th className="p-3">Enabled</th></tr>
            </thead>
            <tbody>
              {rules.data?.map((r) => (
                <tr key={r.id} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                  <td className="p-3">{r.serverName ?? 'All servers'}</td>
                  <td className="p-3">{r.metricType}</td>
                  <td className="p-3">
                    {r.operator === 'GreaterThan' ? '>' : '<'}{' '}
                    {isAdmin ? (
                      <input type="number" aria-label={`Threshold for rule ${r.id}`} defaultValue={r.threshold}
                        className="w-24 rounded border border-slate-300 bg-white px-2 py-1 dark:border-slate-700 dark:bg-slate-900"
                        onBlur={(e) => {
                          const v = Number(e.target.value)
                          if (!Number.isNaN(v) && v !== r.threshold) updateRule.mutate({ id: r.id, operator: r.operator, threshold: v, isEnabled: r.isEnabled })
                        }} />
                    ) : r.threshold}
                  </td>
                  <td className="p-3">
                    <input type="checkbox" aria-label={`Rule ${r.id} enabled`} checked={r.isEnabled} disabled={!isAdmin}
                      onChange={(e) => updateRule.mutate({ id: r.id, operator: r.operator, threshold: r.threshold, isEnabled: e.target.checked })} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
        {isAdmin && <p className="mt-2 text-xs text-slate-500">Edit a threshold and click outside the field to save. New rules can be created through the API (POST /api/v1/alert-rules).</p>}
      </section>
    </div>
  )
}