import type { ReactNode } from 'react'
import { Link, Outlet } from 'react-router'

// The frame around every page; the current route renders in <Outlet />.
export function Layout({ actions }: { actions?: ReactNode }) {
  return (
    <div className="min-h-screen">
      <header className="border-b border-gray-200">
        <nav className="mx-auto flex max-w-md items-center justify-between px-8 py-4">
          <Link to="/" className="font-semibold">
            Todos
          </Link>
          {actions}
        </nav>
      </header>
      <main>
        <Outlet />
      </main>
    </div>
  )
}
