import { expect, type Page } from '@playwright/test'

const mailpit = process.env.MAILPIT_URL ?? 'http://localhost:8025'

export const password = 'P@ssword123!'

// A new, unique email address for each test, so tests never share an account.
export function newEmail(prefix = 'user'): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.com`
}

// Waits for an email to this address in Mailpit (which the app sends to over SMTP) and returns
// the first link in it to the given page, such as "/confirm-email".
export async function linkFromEmail(to: string, page: string): Promise<string> {
  let link: string | undefined
  await expect
    .poll(
      async () => {
        const search = await fetch(`${mailpit}/api/v1/search?query=${encodeURIComponent(`to:"${to}"`)}`)
        const { messages } = (await search.json()) as { messages: { ID: string }[] }
        for (const { ID } of messages) {
          const message = (await (await fetch(`${mailpit}/api/v1/message/${ID}`)).json()) as { Text: string }
          link = message.Text.match(/https?:\/\/\S+/g)?.find((url) => new URL(url).pathname === page)
          if (link) return true
        }
        return false
      },
      { message: `an email to ${to} with a ${page} link`, timeout: 20_000 },
    )
    .toBe(true)
  return link!
}

export async function register(page: Page, email: string) {
  await page.goto('/register')
  await page.getByPlaceholder('Email').fill(email)
  await page.getByPlaceholder('Password').fill(password)
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page.getByText(`sent a confirmation link to ${email}`)).toBeVisible()
}

export async function confirmEmail(page: Page, email: string) {
  await page.goto(await linkFromEmail(email, '/confirm-email'))
  await expect(page.getByText('Your email address is confirmed.')).toBeVisible()
}

export async function logIn(page: Page, email: string, pass = password) {
  await page.goto('/login')
  await page.getByPlaceholder('Email').fill(email)
  await page.getByPlaceholder('Password').fill(pass)
  await page.getByRole('button', { name: 'Log in', exact: true }).click()
}

// Opens a signed-in page through the header's navigation. (page.goto would reload the app, and a
// reload signs out: tokens are kept in memory only.)
export async function openFromNav(page: Page, name: 'Todos' | 'Settings' | 'Admin') {
  await page.getByRole('banner').getByRole('navigation').getByRole('link', { name, exact: true }).click()
}

export async function logOut(page: Page) {
  // The user menu: the button with the user's name in the header.
  await page.getByRole('banner').locator('[data-slot="dropdown-menu-trigger"]').click()
  await page.getByRole('menuitem', { name: 'Log out' }).click()
  await expect(page).toHaveURL(/\/login$/)
}

// Registers, confirms the email from the real email, and logs in, ending on the Todos page.
export async function signUp(page: Page, email = newEmail()): Promise<string> {
  await register(page, email)
  await confirmEmail(page, email)
  await logIn(page, email)
  await expect(page.getByRole('heading', { name: 'Todos' })).toBeVisible()
  return email
}

// The admin: the app must list E2E_ADMIN_EMAIL in Accounts:AdminEmails. Against the Development
// app, use the demo user: E2E_ADMIN_EMAIL=demo@example.com E2E_ADMIN_PASSWORD=Demo123!
export const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin-e2e@example.com'
export const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? password

// Logs in as the admin, signing up first if the account doesn't exist yet (the first test to run).
export async function logInAsAdmin(page: Page) {
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
}
