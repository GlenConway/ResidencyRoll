import type { ComponentProps } from 'react'
import { cn } from 'cn'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export function Field({
  id,
  label,
  className,
  ...props
}: { id: string; label: string; className?: string } & ComponentProps<typeof Input>) {
  return (
    <div className={cn('flex min-w-0 flex-col gap-1.5', className)}>
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} {...props} />
    </div>
  )
}
