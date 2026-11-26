import { Link, useLocation } from 'react-router'

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
        <p role="status" className="mb-6 rounded border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-800">
          {state.notice}
        </p>
      )}
      <h1 className="mb-3 text-3xl font-semibold">Welcome to ApiHost</h1>
      <p className="mb-8 max-w-xl text-gray-600">
        A starting point for your app. The Todos page shows the patterns to build on: validated
        endpoints, background messages, and live updates across open tabs.
      </p>
      <Link to="/todos" className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">
        Open your todos
      </Link>
    </div>
  )
}
