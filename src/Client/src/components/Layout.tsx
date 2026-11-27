import type { ReactNode } from 'react'
import { Link, NavLink, Outlet } from 'react-router'
import { cn } from '@/lib/utils'
import { appName, PageMeta } from './PageMeta'

// A link in the header's navigation; highlighted on its current page.
export function NavItem({ to, children }: { to: string; children: ReactNode }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        cn('rounded-md px-2 py-1 text-sm', isActive ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:text-foreground')
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
    <div className="flex min-h-screen flex-col bg-muted/40">
      <PageMeta />
      <header className="border-b bg-background">
        <div className="relative mx-auto flex max-w-3xl items-center gap-4 px-6 py-3">
          <Link to="/" className="font-semibold">
            {appName}
          </Link>
          <nav className="flex flex-1 items-center gap-1">{nav}</nav>
          {actions}
        </div>
      </header>
      <main className="flex-1">
        <Outlet />
      </main>
      <footer className="border-t bg-background">
        <div className="mx-auto max-w-3xl px-6 py-4 text-sm text-muted-foreground">
          {appName} · Built with Wolverine and React
        </div>
      </footer>
    </div>
  )
}
