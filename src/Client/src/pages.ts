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
  { path: '/todos', index: false },
]
