import { expect, test, type Page } from '@playwright/test'
import { linkFromEmail, logInAsAdmin, newEmail, openFromNav, password, signUp } from './helpers'

// The back office's Users section (with organisations, the back office opens on its dashboard).
async function openUsers(page: Page) {
  await openFromNav(page, 'Admin')
  await expect(page.getByRole('heading', { name: 'Admin', exact: true })).toBeVisible()
  await page.getByRole('navigation', { name: 'Admin' }).getByRole('link', { name: 'Users' }).click()
}


test('an admin invites someone, who chooses a password from the email and is signed in', async ({ page, browser }) => {
  // The admin account may exist from an earlier run; sign up once, otherwise just log in.
  await logInAsAdmin(page)

  await openUsers(page)

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
  await openUsers(page)
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
