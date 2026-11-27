import type { ReactNode } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'

// A titled part of a settings-style page (Settings, Organisation, Admin): a card with an <h2>.
// tone "danger": leaving, deleting and other things that can't be undone.
export function Section({
  title,
  description,
  children,
  tone,
}: {
  title: string
  description?: ReactNode
  children: ReactNode
  tone?: 'danger'
}) {
  return (
    <section>
      <Card className={cn(tone === 'danger' && 'border-destructive/40')}>
      <CardHeader>
        <CardTitle>
          <h2 className={cn('text-lg font-semibold', tone === 'danger' && 'text-destructive')}>{title}</h2>
        </CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent>{children}</CardContent>
      </Card>
    </section>
  )
}
