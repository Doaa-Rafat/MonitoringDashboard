import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { getErrorMessage } from '@/api/errors'
import { Badge, statusTone } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ErrorMessage } from '@/components/ui/ErrorMessage'
import { Spinner } from '@/components/ui/Spinner'
import { TextField } from '@/components/ui/TextField'
import { serversApi } from '@/features/servers/api'
import { fmtDateTime } from '@/lib/format'
import { reportsApi } from './api'

const pad = (n: number) => String(n).padStart(2, '0')
const toLocalInput = (d: Date) =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`

const schema = z
  .object({
    serverId: z.string().min(1, 'Select a server'),
    startTime: z.string().min(1, 'Choose a start time'),
    endTime: z.string().min(1, 'Choose an end time'),
  })
  .refine((v) => !v.startTime || !v.endTime || new Date(v.startTime) < new Date(v.endTime), {
    message: 'Start must be earlier than end', path: ['endTime'],
  })
type Values = z.infer<typeof schema>

export function ReportsPage() {
  const qc = useQueryClient()
  const [page, setPage] = useState(1)

  const servers = useQuery({ queryKey: ['servers'], queryFn: serversApi.list })
  const reports = useQuery({
    queryKey: ['reports', page],
    queryFn: () => reportsApi.list(page),
    placeholderData: keepPreviousData,
    // Fallback polling while something is running, in case a real-time event is missed.
    refetchInterval: (q) => (q.state.data?.items.some((r) => r.status === 'Pending' || r.status === 'Processing') ? 4000 : false),
  })

  const { register, handleSubmit, formState: { errors } } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { serverId: '', startTime: toLocalInput(new Date(Date.now() - 3 * 3_600_000)), endTime: toLocalInput(new Date()) },
  })

  const request = useMutation({
    mutationFn: (v: Values) => reportsApi.request({
      serverId: Number(v.serverId),
      startTime: new Date(v.startTime).toISOString(),
      endTime: new Date(v.endTime).toISOString(),
    }),
    onSuccess: () => { setPage(1); void qc.invalidateQueries({ queryKey: ['reports'] }) },
  })
  const download = useMutation({ mutationFn: reportsApi.download })

  const data = reports.data

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Reports</h1>

      <Card>
        <form onSubmit={handleSubmit((v) => request.mutate(v))} noValidate className="space-y-3">
          <h2 className="font-medium">Generate a performance report</h2>
          {request.isError && <ErrorMessage message={getErrorMessage(request.error)} />}
          <div className="grid gap-3 sm:grid-cols-3">
            <div>
              <label htmlFor="serverId" className="mb-1 block text-sm font-medium">Server</label>
              <select id="serverId" {...register('serverId')}
                className="block w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-900">
                <option value="">Select...</option>
                {servers.data?.items.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
              {errors.serverId && <p role="alert" className="mt-1 text-sm text-red-600">{errors.serverId.message}</p>}
            </div>
            <TextField label="From" type="datetime-local" error={errors.startTime?.message} {...register('startTime')} />
            <TextField label="To" type="datetime-local" error={errors.endTime?.message} {...register('endTime')} />
          </div>
          <Button type="submit" loading={request.isPending}>Generate report</Button>
        </form>
      </Card>

      {reports.isPending && <Spinner className="text-indigo-600" />}
      {reports.isError && <ErrorMessage message={getErrorMessage(reports.error)} />}
      {download.isError && <ErrorMessage message={getErrorMessage(download.error)} />}

      {data && (
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead className="border-b border-slate-200 text-slate-500 dark:border-slate-800">
              <tr><th className="p-3">#</th><th className="p-3">Server</th><th className="p-3">Period</th><th className="p-3">Status</th><th className="p-3">Requested</th><th className="p-3"></th></tr>
            </thead>
            <tbody>
              {data.items.length === 0 && <tr><td colSpan={6} className="p-4 text-slate-500">No reports yet.</td></tr>}
              {data.items.map((r) => (
                <tr key={r.id} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                  <td className="p-3">{r.id}</td>
                  <td className="p-3">{r.serverName}</td>
                  <td className="p-3">{fmtDateTime(r.startTime)} to {fmtDateTime(r.endTime)}</td>
                  <td className="p-3"><Badge tone={statusTone(r.status)}>{r.status}</Badge></td>
                  <td className="p-3">{fmtDateTime(r.createdAt)}</td>
                  <td className="p-3">
                    {r.isDownloadable && <Button variant="secondary" onClick={() => download.mutate(r)}>Download</Button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="flex items-center justify-between p-3 text-sm">
            <span>Page {data.page} of {Math.max(data.totalPages, 1)}</span>
            <span className="space-x-2">
              <Button variant="secondary" disabled={!data.hasPrevious} onClick={() => setPage((p) => p - 1)}>Previous</Button>
              <Button variant="secondary" disabled={!data.hasNext} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </span>
          </div>
        </Card>
      )}
    </div>
  )
}