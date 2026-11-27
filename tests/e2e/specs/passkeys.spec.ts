import { expect, test } from '@playwright/test'
import { logOut, openFromNav, signUp } from './helpers'

// Passkeys, with Chrome's virtual authenticator standing in for a fingerprint or security key.

test('a passkey added in Settings logs in without a password', async ({ page }) => {
  const cdp = await page.context().newCDPSession(page)
  await cdp.send('WebAuthn.enable')
  await cdp.send('WebAuthn.addVirtualAuthenticator', {
    options: {
      protocol: 'ctap2',
      transport: 'internal',
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
    },
  })

  await signUp(page)
  await openFromNav(page, 'Settings')
  await page.getByPlaceholder('Name, e.g. Laptop').fill('Virtual key')
  await page.getByRole('button', { name: 'Add a passkey' }).click()
  await expect(page.getByText('Passkey added.')).toBeVisible()
  await expect(page.getByText('Virtual key')).toBeVisible()

  await logOut(page)

  await page.getByRole('button', { name: 'Log in with a passkey' }).click()
  // Back to Settings, the page the user was on when they logged out.
  await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()
  await expect(page.getByText('Virtual key')).toBeVisible()
})
