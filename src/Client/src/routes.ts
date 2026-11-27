import { index, route, type RouteConfig } from '@react-router/dev/routes'

// The app's pages: each URL and the route module (in routes/) that renders it. A route module
// default-exports the page's component, and its `handle` gives the page title and whether search
// engines may index it (components/PageMeta.tsx). Each page's code loads when it's first visited.
// When you add a page, add its path to pages.ts too.
export default [
  index('routes/home.tsx'),
  route('todos', 'routes/todos.tsx'),
  route('*', 'routes/not-found.tsx'),
] satisfies RouteConfig
