import { expect, test } from '@playwright/test'
import { linkFromEmail, newEmail, openFromNav, password, signUp } from './helpers'

// The admin page. The app must list E2E_ADMIN_EMAIL in Accounts:AdminEmails. Against the
// Development app, use the demo user: E2E_ADMIN_EMAIL=demo@example.com E2E_ADMIN_PASSWORD=Demo123!
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin-e2e@example.com'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? password

test('an admin invites someone, who chooses a password from the email and is signed in', async ({ page, browser }) => {
  // The admin account may exist from an earlier run; sign up once, otherwise just log in.
  await page.goto('/login')
  await page.getByPlaceholder('Email').fill(adminEmail)
  await page.getByPlaceholder('Password').fill(adminPassword)
  await page.getByRole('button', { name: 'Log in', exact: true }).click()
  const loggedIn = await page
    .getByRole('heading', { name: 'Todos' })
    .waitFor({ timeout: 5_000 })
    .then(() => true, () => false)
  if (!loggedIn) {
    await signUp(page, adminEmail)
  }

  await openFromNav(page, 'Admin')
  await expect(page.getByRole('heading', { name: 'Admin' })).toBeVisible()

  const invited = newEmail('invited')
  const invite = page.locator('section').filter({ has: page.getByRole('heading', { name: 'Invite someone' }) })
  await invite.getByPlaceholder('Email').fill(invited)
  await invite.getByRole('button', { name: 'Send invitation' }).click()
  await expect(page.getByText(`Invitation sent to ${invited}.`)).toBeVisible()

  await page.getByPlaceholder('Search by email').fill(invited)
  await expect(page.getByRole('listitem').filter({ hasText: invited }).getByText('Invited', { exact: true })).toBeVisible()

  const guest = await (await browser.newContext()).newPage()
  await guest.goto(await linkFromEmail(invited, '/accept-invitation'))
  await guest.getByPlaceholder('Password', { exact: true }).fill(password)
  await guest.getByPlaceholder('Confirm password').fill(password)
  await guest.getByRole('button', { name: 'Create my account' }).click()
  await expect(guest.getByRole('heading', { name: 'Todos' })).toBeVisible()
  // Not an admin.
  await expect(guest.getByRole('link', { name: 'Admin' })).toHaveCount(0)

  // The admin locks the new account; it can no longer log in.
  await openFromNav(page, 'Todos')
  await openFromNav(page, 'Admin')
  await page.getByPlaceholder('Search by email').fill(invited)
  const row = page.getByRole('listitem').filter({ hasText: invited })
  await row.getByRole('button', { name: 'Lock' }).click()
  await expect(row.getByText('Locked', { exact: true })).toBeVisible()
})

test("the admin page isn't available to other users", async ({ page }) => {
  await signUp(page)
  await expect(page.getByRole('link', { name: 'Admin' })).toHaveCount(0)
  // Navigate inside the app (page.goto would reload it, which signs out).
  await page.evaluate(() => {
    window.history.pushState(null, '', '/admin')
    window.dispatchEvent(new PopStateEvent('popstate'))
  })
  await expect(page.getByText(/not found/i)).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Admin' })).toHaveCount(0)
})
