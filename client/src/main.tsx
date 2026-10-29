import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App'
import { initializeSession } from '@/features/auth/session'
import { initTheme } from '@/features/theme/themeStore'

initTheme()
void initializeSession() // outside React on purpose: StrictMode would run an effect twice

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)