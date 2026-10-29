import type { ReactNode } from 'react'
import { cn } from '@/lib/cn'

export type Tone = 'green' | 'red' | 'amber' | 'blue' | 'slate'

const tones: Record<Tone, string> = {
  green: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300',
  red: 'bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300',
  amber: 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300',
  blue: 'bg-sky-100 text-sky-800 dark:bg-sky-950 dark:text-sky-300',
  slate: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
}

export function Badge({ tone = 'slate', children }: { tone?: Tone; children: ReactNode }) {
  return <span className={cn('inline-block rounded-full px-2 py-0.5 text-xs font-medium', tones[tone])}>{children}</span>
}

export const statusTone = (status: string): Tone =>
  ({ Up: 'green', Down: 'red', Triggered: 'red', Acknowledged: 'amber', Resolved: 'green',
     Pending: 'slate', Processing: 'blue', Completed: 'green', Failed: 'red', Succeeded: 'green' } as Record<string, Tone>)[status] ?? 'slate'