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
]
