// Every page's path. The API reads this list (built into client-routes.json) to answer 404 for
// any other path, and lists the `index` ones in sitemap.xml. Keep it in step with the routes in
// router.tsx; routes.test.ts checks that they match.
export interface ClientRoute {
  path: string
  // Listed in the sitemap for search engines (the route's handle should say index: true too).
  index: boolean
}

export const clientRoutes: ClientRoute[] = [
  { path: '/', index: true },
  { path: '/todos', index: false },
  { path: '/login', index: false },
  { path: '/register', index: false },
  { path: '/forgot-password', index: false },
  { path: '/reset-password', index: false },
  { path: '/confirm-email', index: false },
  { path: '/confirm-email-change', index: false },
  { path: '/accept-invitation', index: false },
  { path: '/external-login', index: false },
  { path: '/settings', index: false },
  { path: '/admin', index: false },
]
