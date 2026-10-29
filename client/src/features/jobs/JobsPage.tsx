import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { http } from '@/api/http'
import { getErrorMessage } from '@/api/errors'
import type { JobCountsDto, JobDto, PagedResult, RecurringJobDto } from '@/api/types'
import { Badge, statusTone } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ErrorMessage } from '@/components/ui/ErrorMessage'
import { fmtDateTime } from '@/lib/format'

const states = ['Failed', 'Succeeded', 'Processing', 'Scheduled', 'Enqueued', 'Deleted']

const jobsApi = {
  counts: () => http.get<JobCountsDto>('/jobs/counts').then((r) => r.data),
  recurring: () => http.get<RecurringJobDto[]>('/jobs/recurring').then((r) => r.data),
  list: (state: string, page: number) =>
    http.get<PagedResult<JobDto>>('/jobs', { params: { state, page, pageSize: 10 } }).then((r) => r.data),
  retry: (id: string) => http.post(`/jobs/${id}/retry`),
}

export function JobsPage() {
  const qc = useQueryClient()
  const [state, setState] = useState('Failed')
  const [page, setPage] = useState(1)

  const counts = useQuery({ queryKey: ['jobs', 'counts'], queryFn: jobsApi.counts, refetchInterval: 5000 })
  const recurring = useQuery({ queryKey: ['jobs', 'recurring'], queryFn: jobsApi.recurring, refetchInterval: 10_000 })
  const jobs = useQuery({
    queryKey: ['jobs', 'list', state, page],
    queryFn: () => jobsApi.list(state, page),
    refetchInterval: 5000,
    placeholderData: keepPreviousData,
  })
  const retry = useMutation({ mutationFn: jobsApi.retry, onSuccess: () => void qc.invalidateQueries({ queryKey: ['jobs'] }) })

  const c = counts.data
  const stats: [string, number | undefined][] = [
    ['Succeeded', c?.succeeded], ['Failed', c?.failed], ['Processing', c?.processing],
    ['Scheduled', c?.scheduled], ['Enqueued', c?.enqueued], ['Recurring', c?.recurring],
  ]

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-2xl font-semibold">Background jobs</h1>
        <a href="/hangfire" target="_blank" rel="noreferrer" className="text-sm text-indigo-600 hover:underline dark:text-indigo-400">Open Hangfire dashboard</a>
      </div>

      <section aria-label="Totals" className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {stats.map(([label, value]) => (
          <Card key={label}><p className="text-xs text-slate-500">{label}</p><p className="text-xl font-semibold">{value ?? '-'}</p></Card>
        ))}
      </section>

      <section aria-label="Recurring jobs">
        <h2 className="mb-2 text-lg font-semibold">Recurring jobs</h2>
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[560px] text-left text-sm">
            <thead className="border-b border-slate-200 text-slate-500 dark:border-slate-800">
              <tr><th className="p-3">Job</th><th className="p-3">Schedule</th><th className="p-3">Last run</th><th className="p-3">Next run</th><th className="p-3">Last state</th></tr>
            </thead>
            <tbody>
              {recurring.data?.map((j) => (
                <tr key={j.id} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                  <td className="p-3">{j.id}</td><td className="p-3 font-mono">{j.cron}</td>
                  <td className="p-3">{fmtDateTime(j.lastExecution)}</td><td className="p-3">{fmtDateTime(j.nextExecution)}</td>
                  <td className="p-3">{j.lastJobState ? <Badge tone={statusTone(j.lastJobState)}>{j.lastJobState}</Badge> : '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      </section>

      <section aria-label="Job history">
        <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-semibold">Jobs</h2>
          <div className="flex flex-wrap gap-1" role="tablist">
            {states.map((s) => (
              <button key={s} role="tab" aria-selected={s === state} onClick={() => { setState(s); setPage(1) }}
                className={`rounded-md px-3 py-1 text-sm ${s === state ? 'bg-indigo-600 text-white' : 'bg-slate-100 dark:bg-slate-800'}`}>{s}</button>
            ))}
          </div>
        </div>
        {retry.isError && <ErrorMessage message={getErrorMessage(retry.error)} />}
        {jobs.isError && <ErrorMessage message={getErrorMessage(jobs.error)} />}
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[560px] text-left text-sm">
            <thead className="border-b border-slate-200 text-slate-500 dark:border-slate-800">
              <tr><th className="p-3">Id</th><th className="p-3">Job</th><th className="p-3">Time</th><th className="p-3">Details</th><th className="p-3"></th></tr>
            </thead>
            <tbody>
              {jobs.data?.items.length === 0 && <tr><td colSpan={5} className="p-4 text-slate-500">No {state.toLowerCase()} jobs.</td></tr>}
              {jobs.data?.items.map((j) => (
                <tr key={j.id} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                  <td className="p-3">{j.id}</td><td className="p-3">{j.name}</td><td className="p-3">{fmtDateTime(j.timestamp)}</td>
                  <td className="max-w-xs truncate p-3" title={j.error ?? ''}>{j.error ?? (j.durationMs !== null ? `${j.durationMs} ms` : '')}</td>
                  <td className="p-3">{state === 'Failed' && <Button variant="secondary" loading={retry.isPending} onClick={() => retry.mutate(j.id)}>Retry</Button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {jobs.data && (
            <div className="flex items-center justify-between p-3 text-sm">
              <span>Page {jobs.data.page} of {Math.max(jobs.data.totalPages, 1)}</span>
              <span className="space-x-2">
                <Button variant="secondary" disabled={!jobs.data.hasPrevious} onClick={() => setPage((p) => p - 1)}>Previous</Button>
                <Button variant="secondary" disabled={!jobs.data.hasNext} onClick={() => setPage((p) => p + 1)}>Next</Button>
              </span>
            </div>
          )}
        </Card>
      </section>
    </div>
  )
}