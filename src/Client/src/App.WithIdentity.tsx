import { Layout } from './components/Layout'
import { AccountActions, Navigation } from './features/account/AccountNavigation'
import { AuthProvider } from './features/auth/AuthContext'

// The frame around every page (root.tsx renders it): the header, with the navigation and user
// menu of whoever is signed in, the current page and the footer.
export function App() {
  return (
    <AuthProvider>
      <Layout nav={<Navigation />} actions={<AccountActions />} />
    </AuthProvider>
  )
}
