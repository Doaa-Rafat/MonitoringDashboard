import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '@/components/ui/Button'
import { ErrorMessage } from '@/components/ui/ErrorMessage'
import { TextField } from '@/components/ui/TextField'
import type { ServerInput } from './api'

const schema = z.object({
  name: z.string().trim().min(1, 'Enter a name').max(100, 'At most 100 characters'),
  ipAddress: z.string().trim().max(50, 'At most 50 characters'),
  description: z.string().trim().max(250, 'At most 250 characters'),
})
type Values = z.infer<typeof schema>

interface Props {
  initial?: ServerInput
  submitLabel: string
  submitting: boolean
  error?: string | null
  onSubmit: (v: ServerInput) => void
  onCancel: () => void
}

export function ServerForm({ initial, submitLabel, submitting, error, onSubmit, onCancel }: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: initial?.name ?? '', ipAddress: initial?.ipAddress ?? '', description: initial?.description ?? '' },
  })

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-3">
      {error && <ErrorMessage message={error} />}
      <div className="grid gap-3 sm:grid-cols-3">
        <TextField label="Name" error={errors.name?.message} {...register('name')} />
        <TextField label="IP address" placeholder="10.0.0.1" error={errors.ipAddress?.message} {...register('ipAddress')} />
        <TextField label="Description" error={errors.description?.message} {...register('description')} />
      </div>
      <div className="flex gap-2">
        <Button type="submit" loading={submitting}>{submitLabel}</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  )
}