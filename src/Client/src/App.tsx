import { Layout, NavItem } from './components/Layout'

// The frame around every page (root.tsx renders it): the header, the current page and the footer.
export function App() {
  return <Layout nav={<NavItem to="/todos">Todos</NavItem>} />
}
