import { expect, test } from '@playwright/test'

// What search engines see: page titles, which pages may be indexed, and real 404s.

test('the home page may be indexed, with a canonical URL and a description', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', /\/$/)
  await expect(page.locator('meta[name="robots"]')).toHaveCount(0)
  await expect(page.locator('meta[name="description"]')).toHaveAttribute('content', /.+/)
})

test('an unknown page answers 404, shows the not-found page, and is kept out of search results', async ({ page }) => {
  const response = await page.goto('/no-such-page')
  expect(response?.status()).toBe(404)
  await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible()
  await expect(page).toHaveTitle(/^Page not found · /)
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex')
})

test('pages other than the home page are kept out of search results', async ({ page }) => {
  await page.goto('/todos')
  // With accounts, /todos sends a signed-out visitor to log in; either way, not indexable.
  await expect(page).toHaveTitle(/ · /)
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex')
})

test('robots.txt and sitemap.xml are served', async ({ request, baseURL }) => {
  const robots = await (await request.get('/robots.txt')).text()
  expect(robots).toContain('Disallow: /api/')
  expect(robots).toContain(`Sitemap: ${baseURL}/sitemap.xml`)
  const sitemap = await (await request.get('/sitemap.xml')).text()
  expect(sitemap).toContain(`<loc>${baseURL}/</loc>`)
})
