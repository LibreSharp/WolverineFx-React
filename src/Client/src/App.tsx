import { createBrowserRouter, RouterProvider } from 'react-router'
import { Layout } from './components/Layout'
import { NotFound } from './components/NotFound'
import { TodoList } from './features/todos/TodoList'

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { index: true, element: <TodoList /> },
      { path: '*', element: <NotFound /> },
    ],
  },
])

export default function App() {
  return <RouterProvider router={router} />
}
