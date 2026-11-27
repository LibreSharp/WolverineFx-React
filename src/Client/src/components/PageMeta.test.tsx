import { waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderRoutes } from '../test/render'
import { Layout } from './Layout'
import { appName, type PageHandle } from './PageMeta'

const routes = [
  {
    element: <Layout />,
    children: [
      { index: true, element: <p>Home</p>, handle: { index: true } satisfies PageHandle },
      { path: 'private', element: <p>Private</p>, handle: { title: 'Private' } satisfies PageHandle },
    ],
  },
]

describe('PageMeta', () => {
  it('lets search engines index pages that opt in, with a canonical URL', async () => {
    renderRoutes(routes, '/')
    await waitFor(() => expect(document.title).toBe(appName))
    expect(document.head.querySelector('link[rel="canonical"]')).toHaveAttribute('href', `${window.location.origin}/`)
    expect(document.head.querySelector('meta[name="robots"]')).toBeNull()
  })

  it('keeps every other page out of search results', async () => {
    renderRoutes(routes, '/private')
    await waitFor(() => expect(document.title).toBe(`Private · ${appName}`))
    expect(document.head.querySelector('meta[name="robots"]')).toHaveAttribute('content', 'noindex')
    expect(document.head.querySelector('link[rel="canonical"]')).toBeNull()
  })
})
