import { Link } from 'react-router'
import { Button } from '@/components/ui/button'

export function NotFound() {
  return (
    <div className="mx-auto max-w-3xl px-6 py-16">
      <h1 className="mb-2 text-2xl font-semibold">Page not found</h1>
      <Button asChild variant="link" className="px-0">
        <Link to="/">Go to the home page</Link>
      </Button>
    </div>
  )
}
