import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import type { OnlineUserDto } from '@/api/types'
import { getValidAccessToken } from '@/features/auth/session'
import { RealtimeContext, type ConnectionState } from './realtimeContext'

const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const [connection, setConnection] = useState<HubConnection | null>(null)
  const [state, setState] = useState<ConnectionState>('connecting')
  const [onlineUsers, setOnlineUsers] = useState<OnlineUserDto[]>([])

  useEffect(() => {
    let disposed = false

    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/monitoring', { accessTokenFactory: getValidAccessToken })
      // Retry forever with exponential backoff (1s, 2s, 4s ... capped at 30s).
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => Math.min(1000 * 2 ** ctx.previousRetryCount, 30_000),
      })
      .configureLogging(LogLevel.Warning)
      .build()

    conn.on('PresenceChanged', (users: OnlineUserDto[]) => setOnlineUsers(users))
    conn.onreconnecting(() => !disposed && setState('reconnecting'))
    conn.onreconnected(() => !disposed && setState('connected'))
    conn.onclose(() => !disposed && setState('disconnected'))

    setConnection(conn)

    // automatic reconnect only applies AFTER a first successful start, so the first start retries by hand.
    const startWithRetry = async () => {
      for (let attempt = 0; !disposed; attempt++) {
        try {
          await conn.start()
          if (!disposed) setState('connected')
          return
        } catch {
          if (disposed) return
          setState('disconnected')
          await delay(Math.min(1000 * 2 ** attempt, 30_000))
        }
      }
    }
    void startWithRetry()

    return () => {
      disposed = true
      void conn.stop()
    }
  }, [])

  const value = useMemo(() => ({ connection, state, onlineUsers }), [connection, state, onlineUsers])
  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>
}