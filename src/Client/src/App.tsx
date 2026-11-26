import { createBrowserRouter, RouterProvider } from 'react-router'
import { HomePage } from './components/HomePage'
import { Layout, NavItem } from './components/Layout'
import { NotFound } from './components/NotFound'
import { TodoList } from './features/todos/TodoList'

const router = createBrowserRouter([
  {
    element: <Layout nav={<NavItem to="/todos">Todos</NavItem>} />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'todos', element: <TodoList /> },
      { path: '*', element: <NotFound /> },
    ],
  },
])

export default function App() {
  return <RouterProvider router={router} />
}
