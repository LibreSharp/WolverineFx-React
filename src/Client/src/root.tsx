import type { ReactNode } from 'react'
import { isRouteErrorResponse, Links, Meta, Scripts, ScrollRestoration } from 'react-router'
import { QueryClientProvider } from '@tanstack/react-query'
import { App } from './App'
import { appName } from './components/PageMeta'
import { Button } from './components/ui/button'
import { queryClient } from './lib/queryClient'
import './index.css'

// The HTML document around every page. The build renders the public pages into HTML with it
// (pre-rendering, see react-router.config.ts), so search engines and link previews see their
// content without running JavaScript; every other page starts from a fallback page that shows
// HydrateFallback until the app loads.
export function Layout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <head>
        <meta charSet="UTF-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
        {/* For search results and link previews. Describe your product here. Each page sets its
            own <title> (components/PageMeta.tsx). */}
        <meta name="description" content="ApiHost: a full-stack app built with Wolverine and React." />
        <meta name="theme-color" content="#4f46e5" />
        <meta property="og:type" content="website" />
        <meta property="og:site_name" content="ApiHost" />
        <meta property="og:title" content="ApiHost" />
        <meta property="og:description" content="ApiHost: a full-stack app built with Wolverine and React." />
        <meta name="twitter:card" content="summary" />
        <Meta />
        <Links />
      </head>
      <body>
        {children}
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  )
}

export default function Root() {
  return (
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  )
}

// What a page that isn't pre-rendered shows until the app has loaded: nothing yet, kept out of
// search results (the page itself says otherwise once it loads, if it may be indexed).
export function HydrateFallback() {
  return (
    <>
      <title>{appName}</title>
      <meta name="robots" content="noindex" />
    </>
  )
}

// An error while rendering, or in loading a page's code (for example after a new deployment).
export function ErrorBoundary({ error }: { error: unknown }) {
  const message = isRouteErrorResponse(error) ? `${error.status} ${error.statusText}` : 'Something went wrong'
  return (
    <div className="mx-auto max-w-3xl px-6 py-16">
      <title>{`${message} · ${appName}`}</title>
      <meta name="robots" content="noindex" />
      <h1 className="mb-2 text-2xl font-semibold">{message}</h1>
      <Button variant="link" className="px-0" onClick={() => window.location.reload()}>
        Reload the page
      </Button>
    </div>
  )
}
