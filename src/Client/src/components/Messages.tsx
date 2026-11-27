import type { ReactNode } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { cn } from '@/lib/utils'

// A request's error, under the form or button that made it. Nothing when there's no error.
export function ErrorText({ error, className }: { error: Error | null | undefined; className?: string }) {
  return error ? (
    <p role="alert" className={cn('text-sm text-destructive', className)}>
      {error.message}
    </p>
  ) : null
}

// A message on its own line: a confirmation ("Check your email"), a warning, or an error.
// Confirmations and warnings are announced politely (role "status"); errors at once ("alert").
export function Notice({
  tone = 'success',
  children,
  className,
}: {
  tone?: 'success' | 'warning' | 'error'
  children: ReactNode
  className?: string
}) {
  return (
    <Alert
      role={tone === 'error' ? 'alert' : 'status'}
      variant={tone === 'error' ? 'destructive' : tone}
      className={className}
    >
      <AlertDescription>{children}</AlertDescription>
    </Alert>
  )
}
