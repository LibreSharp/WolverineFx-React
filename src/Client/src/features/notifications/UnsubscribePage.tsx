import { Link, useSearchParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { AuthCard } from '../auth/AuthCard'
import { useUnsubscribe } from './notificationQueries'

// /unsubscribe?token=...: the link at the bottom of a notification email. It needs no login, and
// asks first, so a mail scanner that opens links doesn't unsubscribe anyone. (Mail apps' own
// "Unsubscribe" button posts straight to the API.)
export function UnsubscribePage() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const unsubscribe = useUnsubscribe()

  return (
    <AuthCard title="Unsubscribe">
      {unsubscribe.isSuccess ? (
        <p role="status">
          You won't get emails about “{unsubscribe.data.description.toLowerCase()}” any more. They still appear under the bell
          when you're logged in. To change this later, go to <Link to="/settings" className="underline">Settings</Link>.
        </p>
      ) : (
        <>
          <p className="mb-6 text-muted-foreground">Stop getting this kind of notification by email?</p>
          {unsubscribe.isError && <p role="alert" className="mb-4 text-sm text-destructive">{unsubscribe.error.message}</p>}
          <Button disabled={token === '' || unsubscribe.isPending} onClick={() => unsubscribe.mutate(token)}>
            Unsubscribe
          </Button>
        </>
      )}
    </AuthCard>
  )
}
