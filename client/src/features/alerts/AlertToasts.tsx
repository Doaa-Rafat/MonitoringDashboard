import { useState } from 'react'
import type { AlertDto } from '@/api/types'
import { fmtNum } from '@/lib/format'
import { useHubEvent } from '@/realtime/realtimeContext'

export function AlertToasts() {
  const [toasts, setToasts] = useState<AlertDto[]>([])

  useHubEvent('AlertTriggered', (a) => {
    setToasts((t) => [a, ...t].slice(0, 3))
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== a.id)), 8000)
  })

  return (
    <div className="fixed right-4 top-16 z-50 w-80 max-w-[calc(100vw-2rem)] space-y-2">
      {toasts.map((a) => (
        <div key={a.id} role="alert" className="rounded-md border border-red-300 bg-red-50 p-3 text-sm text-red-900 shadow dark:border-red-900 dark:bg-red-950 dark:text-red-200">
          <b>Alert on {a.serverName}</b>
          <p>{a.metricType}: {fmtNum(a.metricValue)} (threshold {fmtNum(a.threshold)})</p>
        </div>
      ))}
    </div>
  )
}