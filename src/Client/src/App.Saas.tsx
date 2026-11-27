import { Layout, NavItem } from './components/Layout'
import { AccountActions, Navigation } from './features/account/AccountNavigation'
import { AuthProvider } from './features/auth/AuthContext'
import { OrganisationSwitcher } from './features/organisations/OrganisationSwitcher'

// The frame around every page (root.tsx renders it): the header, with the navigation, the
// organisation switcher and the user menu of whoever is signed in, the current page and the
// footer.
export function App() {
  return (
    <AuthProvider>
      <Layout
        nav={
          <>
            <Navigation />
            <NavItem to="/pricing">Pricing</NavItem>
          </>
        }
        actions={
          <div className="flex items-center gap-4">
            <OrganisationSwitcher />
            <AccountActions />
          </div>
        }
      />
    </AuthProvider>
  )
}
