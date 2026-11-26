import type { ReactNode } from 'react'
import { Link, NavLink, Outlet } from 'react-router'

// A link in the header's navigation; highlighted on its current page.
export function NavItem({ to, children }: { to: string; children: ReactNode }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        `rounded px-2 py-1 text-sm ${isActive ? 'bg-indigo-50 text-indigo-700' : 'text-gray-600 hover:text-gray-900'}`
      }
    >
      {children}
    </NavLink>
  )
}

// The frame around every page: a header with the app name, navigation and (optionally) account
// actions, the current route's page in <Outlet />, and a footer.
export function Layout({ nav, actions }: { nav?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col bg-gray-50 text-gray-900">
      <header className="border-b border-gray-200 bg-white">
        <div className="mx-auto flex max-w-3xl items-center gap-4 px-6 py-3">
          <Link to="/" className="font-semibold">
            ApiHost
          </Link>
          <nav className="flex flex-1 items-center gap-1">{nav}</nav>
          {actions}
        </div>
      </header>
      <main className="flex-1">
        <Outlet />
      </main>
      <footer className="border-t border-gray-200 bg-white">
        <div className="mx-auto max-w-3xl px-6 py-4 text-sm text-gray-500">
          ApiHost · Built with Wolverine and React
        </div>
      </footer>
    </div>
  )
}
