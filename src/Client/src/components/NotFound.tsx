import { Link } from 'react-router'

export function NotFound() {
  return (
    <div className="mx-auto max-w-md p-8">
      <h1 className="mb-2 text-2xl font-semibold">Page not found</h1>
      <Link to="/" className="text-indigo-600 hover:underline">
        Back to your todos
      </Link>
    </div>
  )
}
