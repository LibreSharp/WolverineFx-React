import { NavLink, Outlet } from 'react-router'
import { cn } from '@/lib/utils'
import { adminSections } from './adminSections'

// The frame of the back office (/admin): its sections as tabs, and the current one's page. Only
// for the Admin role (RequireAdmin in routes.ts); the API checks the role on every request too.
export function AdminLayout() {
  return (
    <div className="mx-auto max-w-5xl space-y-6 px-6 py-10">
      <h1 className="text-2xl font-semibold">Admin</h1>
      <nav aria-label="Admin" className="flex flex-wrap gap-1 border-b">
        {adminSections.map((section) => (
          <NavLink
            key={section.to}
            to={section.to}
            end={section.end}
            className={({ isActive }) =>
              cn(
                '-mb-px border-b-2 px-3 py-2 text-sm',
                isActive ? 'border-primary font-medium text-foreground' : 'border-transparent text-muted-foreground hover:text-foreground',
              )
            }
          >
            {section.label}
          </NavLink>
        ))}
      </nav>
      <Outlet />
    </div>
  )
}
