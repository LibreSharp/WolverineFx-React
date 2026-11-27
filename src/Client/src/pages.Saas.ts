// Every page's path, for the API and the build. The API reads this list (built into
// client-routes.json) to answer 404 for any other path, and lists the `index` pages in
// sitemap.xml; the build pre-renders the `index` pages into HTML (react-router.config.ts). Keep it
// in step with the routes in routes.ts; pages.test.ts checks that they match.
export interface Page {
  path: string
  // Public, for search engines: listed in the sitemap and pre-rendered. The route's handle should
  // say index: true too.
  index: boolean
}

export const pages: Page[] = [
  { path: '/', index: true },
  { path: '/pricing', index: true },
  { path: '/todos', index: false },
  { path: '/login', index: false },
  { path: '/register', index: false },
  { path: '/forgot-password', index: false },
  { path: '/reset-password', index: false },
  { path: '/confirm-email', index: false },
  { path: '/confirm-email-change', index: false },
  { path: '/accept-invitation', index: false },
  { path: '/external-login', index: false },
  { path: '/unsubscribe', index: false },
  { path: '/settings', index: false },
  { path: '/admin', index: false },
  { path: '/admin/organisations', index: false },
  { path: '/admin/organisations/:id', index: false },
  { path: '/admin/users', index: false },
  { path: '/admin/features', index: false },
  { path: '/admin/audit', index: false },
  { path: '/admin/failed-messages', index: false },
  { path: '/organisation', index: false },
  { path: '/organisation/integrations', index: false },
  { path: '/join', index: false },
]
