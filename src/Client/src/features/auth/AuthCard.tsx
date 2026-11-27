import type { ReactNode } from 'react'
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'

// The frame of the signed-out pages (log in, register, reset a password...): a narrow card with
// the page's heading, its content, and links to the other pages underneath.
export function AuthCard({ title, children, footer }: { title: string; children: ReactNode; footer?: ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-sm px-4 py-10">
      <Card>
        <CardHeader>
          <CardTitle>
            <h1 className="text-2xl font-semibold">{title}</h1>
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">{children}</CardContent>
        {footer && <CardFooter className="flex-col gap-1 text-sm text-muted-foreground">{footer}</CardFooter>}
      </Card>
    </div>
  )
}
