import { useQueryClient } from '@tanstack/react-query'
import { useHubEvent } from './realtimeContext'

/** Refetches the affected lists when the server pushes a change. */
export function useLiveInvalidation() {
  const qc = useQueryClient()
  useHubEvent('AlertTriggered', () => void qc.invalidateQueries({ queryKey: ['alerts'] }))
  useHubEvent('AlertUpdated', () => void qc.invalidateQueries({ queryKey: ['alerts'] }))
  useHubEvent('ReportUpdated', () => void qc.invalidateQueries({ queryKey: ['reports'] }))
}