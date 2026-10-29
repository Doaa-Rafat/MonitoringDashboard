import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { http } from '@/api/http'
import { getErrorMessage } from '@/api/errors'
import type { ManagedUserDto } from '@/api/types'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ErrorMessage } from '@/components/ui/ErrorMessage'
import { Spinner } from '@/components/ui/Spinner'
import { useAuthStore } from '@/features/auth/authStore'
import { fmtDateTime } from '@/lib/format'

const usersApi = {
  list: () => http.get<ManagedUserDto[]>('/users').then((r) => r.data),
  setRoles: (id: number, roles: string[]) => http.put<ManagedUserDto>(`/users/${id}/roles`, { roles }).then((r) => r.data),
}

export function UsersPage() {
  const qc = useQueryClient()
  const me = useAuthStore((s) => s.user?.id)
  const users = useQuery({ queryKey: ['users'], queryFn: usersApi.list })
  const setRoles = useMutation({
    mutationFn: (v: { id: number; roles: string[] }) => usersApi.setRoles(v.id, v.roles),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['users'] }),
  })

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Users</h1>
      {users.isPending && <Spinner className="text-indigo-600" />}
      {users.isError && <ErrorMessage message={getErrorMessage(users.error)} />}
      {setRoles.isError && <ErrorMessage message={getErrorMessage(setRoles.error)} />}
      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[560px] text-left text-sm">
          <thead className="border-b border-slate-200 text-slate-500 dark:border-slate-800">
            <tr><th className="p-3">User</th><th className="p-3">Email</th><th className="p-3">Roles</th><th className="p-3">Joined</th><th className="p-3"></th></tr>
          </thead>
          <tbody>
            {users.data?.map((u) => {
              const isAdmin = u.roles.includes('Admin')
              return (
                <tr key={u.id} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                  <td className="p-3">{u.userName}{u.id === me && ' (you)'}</td>
                  <td className="p-3">{u.email}</td>
                  <td className="space-x-1 p-3">{u.roles.map((r) => <Badge key={r} tone={r === 'Admin' ? 'blue' : 'slate'}>{r}</Badge>)}</td>
                  <td className="p-3">{fmtDateTime(u.createdAt)}</td>
                  <td className="p-3">
                    <Button variant="secondary" disabled={u.id === me || setRoles.isPending}
                      onClick={() => setRoles.mutate({ id: u.id, roles: isAdmin ? ['User'] : ['Admin', 'User'] })}>
                      {isAdmin ? 'Remove admin' : 'Make admin'}
                    </Button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </Card>
      <p className="text-xs text-slate-500">A role change takes effect after the user signs in again.</p>
    </div>
  )
}