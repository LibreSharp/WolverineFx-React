import { expect, test } from '@playwright/test'
import { linkFromEmail, logIn, logOut, newEmail, openFromNav, password, register, signUp } from './helpers'

// Accounts: registration with the real confirmation email, sessions, settings and lockout.

test('a new account must confirm its email before logging in', async ({ page }) => {
  const email = newEmail()
  await register(page, email)

  await logIn(page, email)
  await expect(page.getByRole('alert')).toContainText('Confirm your email address first')

  await page.goto(await linkFromEmail(email, '/confirm-email'))
  await expect(page.getByText('Your email address is confirmed.')).toBeVisible()
  await logIn(page, email)
  await expect(page.getByRole('heading', { name: 'Todos' })).toBeVisible()
})

test("a user's todos update live in their other tabs, and other users don't see them", async ({ browser }) => {
  const mine = await browser.newContext()
  const first = await mine.newPage()
  const email = await signUp(first)
  const second = await mine.newPage()
  await logIn(second, email)
  await expect(second.getByRole('heading', { name: 'Todos' })).toBeVisible()

  const title = `Private todo ${Date.now()}`
  await first.getByPlaceholder('What needs doing?').fill(title)
  await first.getByRole('button', { name: 'Add' }).click()
  await expect(second.getByText(title)).toBeVisible()

  const someoneElse = await (await browser.newContext()).newPage()
  await signUp(someoneElse)
  await expect(someoneElse.getByText('Loading…')).toBeHidden()
  await expect(someoneElse.getByText(title)).toHaveCount(0)
})

test('signed-out visitors are sent to log in, then back where they were going', async ({ page }) => {
  const email = await signUp(page)
  await logOut(page)

  await page.goto('/settings')
  await expect(page).toHaveURL(/\/login$/)
  await page.getByPlaceholder('Email').fill(email)
  await page.getByPlaceholder('Password').fill(password)
  await page.getByRole('button', { name: 'Log in', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()
})

test('the display name changed in Settings shows in the header', async ({ page }) => {
  await signUp(page)
  await openFromNav(page, 'Settings')
  await page.getByLabel('Display name (optional)').fill('Ada Lovelace')
  await page.getByRole('button', { name: 'Save profile' }).click()

  await expect(page.getByText('Profile saved.')).toBeVisible()
  await expect(page.getByRole('banner').getByText('Ada Lovelace')).toBeVisible()
})

test('a forgotten password is reset from the emailed link', async ({ page }) => {
  const email = await signUp(page)
  await logOut(page)

  await page.goto('/forgot-password')
  await page.getByPlaceholder('Email').fill(email)
  await page.getByRole('button', { name: 'Send reset link' }).click()
  await expect(page.getByRole('status')).toContainText(email)

  await page.goto(await linkFromEmail(email, '/reset-password'))
  await page.getByPlaceholder('New password', { exact: true }).fill('N3w-P@ssword!')
  await page.getByPlaceholder('Confirm new password').fill('N3w-P@ssword!')
  await page.getByRole('button', { name: 'Set new password' }).click()
  await expect(page.getByText('Your password has been reset.')).toBeVisible()

  await logIn(page, email, 'N3w-P@ssword!')
  await expect(page.getByRole('heading', { name: 'Todos' })).toBeVisible()
})

test('repeated wrong passwords lock the account', async ({ page }) => {
  const email = await signUp(page)
  await logOut(page)

  for (let attempt = 0; attempt < 5; attempt++) {
    await logIn(page, email, 'not-the-password')
    await expect(page.getByRole('alert')).toBeVisible()
  }

  await logIn(page, email)
  await expect(page.getByRole('alert')).toContainText('locked')
  await expect(page.getByRole('link', { name: 'Reset your password' })).toBeVisible()
})
