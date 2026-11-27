import { Link, useLocation } from 'react-router'
import { Button } from '@/components/ui/button'
import { Notice } from './Messages'

// What another page can show here after redirecting (for example after deleting an account).
export interface HomeLocationState {
  notice?: string
}

// The public landing page. Replace it with your own product's introduction.
export function HomePage() {
  const state = useLocation().state as HomeLocationState | null

  return (
    <div className="mx-auto max-w-3xl px-6 py-16">
      {state?.notice && (
        <Notice className="mb-6">{state.notice}</Notice>
      )}
      <h1 className="mb-3 text-3xl font-semibold">Welcome to ApiHost</h1>
      <p className="mb-8 max-w-xl text-muted-foreground">
        A starting point for your app. The Todos page shows the patterns to build on: validated
        endpoints, background messages, and live updates across open tabs.
      </p>
      <Button asChild size="lg">
        <Link to="/todos">Open your todos</Link>
      </Button>
    </div>
  )
}
