import { createContext, useContext, useEffect, useRef } from 'react'
import { HubConnectionState, type HubConnection } from '@microsoft/signalr'
import type { AlertDto, MetricDto, OnlineUserDto, ReportDto } from '@/api/types'

export type ConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export interface RealtimeContextValue {
  connection: HubConnection | null
  state: ConnectionState
  onlineUsers: OnlineUserDto[]
}

export const RealtimeContext = createContext<RealtimeContextValue>({
  connection: null,
  state: 'disconnected',
  onlineUsers: [],
})

export const useRealtime = () => useContext(RealtimeContext)

/** Events pushed by the server (see IMonitoringClient in the API). */
export interface HubEventMap {
  MetricReceived: MetricDto
  AlertTriggered: AlertDto
  AlertUpdated: AlertDto
  ReportUpdated: ReportDto
}

/** Listens to a hub event for as long as the component is mounted. */
export function useHubEvent<K extends keyof HubEventMap>(event: K, handler: (payload: HubEventMap[K]) => void) {
  const { connection } = useRealtime()
  const handlerRef = useRef(handler)

  useEffect(() => {
    handlerRef.current = handler
  })

  useEffect(() => {
    if (!connection) return
    const listener = (payload: HubEventMap[K]) => handlerRef.current(payload)
    connection.on(event, listener)
    return () => connection.off(event, listener)
  }, [connection, event])
}

/**
 * Joins a server-side group (e.g. SubscribeToServer, 5) while mounted.
 * It re-joins after every reconnect, because the server forgets groups when the connection id changes.
 */
export function useHubSubscription(subscribeMethod: string, unsubscribeMethod: string, ...args: unknown[]) {
  const { connection, state } = useRealtime()
  const argsKey = JSON.stringify(args)

  useEffect(() => {
    if (!connection || state !== 'connected') return
    connection.invoke(subscribeMethod, ...args).catch(() => {})
    return () => {
      if (connection.state === HubConnectionState.Connected) {
        connection.invoke(unsubscribeMethod, ...args).catch(() => {})
      }
    }
    // args are tracked through argsKey
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [connection, state, subscribeMethod, unsubscribeMethod, argsKey])
}