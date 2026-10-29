import { create } from 'zustand'

type Theme = 'light' | 'dark'
const KEY = 'md.theme'

function readInitial(): Theme {
  try {
    const saved = localStorage.getItem(KEY)
    if (saved === 'light' || saved === 'dark') return saved
  } catch {
    /* ignore */
  }
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

const apply = (theme: Theme) => document.documentElement.classList.toggle('dark', theme === 'dark')

export const useThemeStore = create<{ theme: Theme; toggle: () => void }>((set, get) => ({
  theme: readInitial(),
  toggle: () => {
    const next: Theme = get().theme === 'dark' ? 'light' : 'dark'
    try { localStorage.setItem(KEY, next) } catch { /* ignore */ }
    apply(next)
    set({ theme: next })
  },
}))

export const initTheme = () => apply(useThemeStore.getState().theme)