import { Link } from 'react-router-dom'

interface StatusPageProps { code: string; title: string; message: string }

export function StatusPage({ code, title, message }: StatusPageProps) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 py-24 text-center">
      <p className="text-5xl font-bold text-indigo-600">{code}</p>
      <h1 className="text-xl font-semibold">{title}</h1>
      <p className="text-slate-600 dark:text-slate-400">{message}</p>
      <Link to="/" className="mt-4 text-indigo-600 hover:underline">Back to the dashboard</Link>
    </div>
  )
}