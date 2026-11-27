import { useMatches } from 'react-router'

// The app's name, shown in every page title.
export const appName = 'ApiHost'

// What a route declares about its page, in its `handle` (see routes/): the title, and whether
// search engines may index it. Pages stay out of search results unless they say `index: true`;
// only public pages worth finding should.
export interface PageHandle {
  title?: string
  index?: boolean
}

// The current page's <title>, robots and canonical tags, from the deepest route with a handle.
// React moves these into the document's <head>, including in the pre-rendered HTML of public
// pages. The default description and link-preview (Open Graph) tags are in root.tsx. The
// canonical URL is relative to the site (search engines resolve it), because the build doesn't
// know the site's address.
export function PageMeta() {
  const matches = useMatches()
  const handle = [...matches].reverse().find((match) => match.handle)?.handle as PageHandle | undefined
  const pathname = matches.at(-1)?.pathname ?? '/'
  const title = handle?.title ? `${handle.title} · ${appName}` : appName

  return (
    <>
      <title>{title}</title>
      {handle?.index ? (
        <link rel="canonical" href={pathname} />
      ) : (
        <meta name="robots" content="noindex" />
      )}
    </>
  )
}
