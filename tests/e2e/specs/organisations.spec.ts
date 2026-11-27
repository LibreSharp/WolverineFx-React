import { expect, test } from '@playwright/test'
import { linkFromEmail, logInAsAdmin, newEmail, openFromNav, password, signUp } from './helpers'

// --Auth saas only (excluded otherwise by template.json): an owner creates an organisation and
// invites a teammate by email; the teammate creates their account from the link, and they share
// the organisation's Todos live, while each keeps their own organisation's apart.
test('a teammate joins from the invitation email and shares the organisation’s todos', async ({ browser }) => {
  const owner = await (await browser.newContext()).newPage()
  await signUp(owner)

  const team = `Team ${Date.now()}`
  await owner.getByRole('link', { name: 'Manage' }).click()
  await owner.getByLabel('New organisation name').fill(team)
  await owner.getByRole('button', { name: 'Create' }).click()
  await expect(owner.getByLabel('Organisation', { exact: true })).toHaveValue(/.+/)
  await expect(owner.getByRole('heading', { name: team })).toBeVisible()

  const teammate = newEmail('teammate')
  await owner.getByLabel('Email to invite').fill(teammate)
  await owner.getByRole('button', { name: 'Invite' }).click()
  await expect(owner.getByText('Invitation sent.')).toBeVisible()

  const guest = await (await browser.newContext()).newPage()
  await guest.goto(await linkFromEmail(teammate, '/join'))
  await expect(guest.getByText(`You're invited to join ${team}`, { exact: false })).toBeVisible()
  await guest.getByLabel('Choose a password').fill(password)
  await guest.getByRole('button', { name: 'Create account and join' }).click()
  await expect(guest.getByRole('heading', { name: 'Todos' })).toBeVisible()

  await openFromNav(owner, 'Todos')
  const title = `Shared todo ${Date.now()}`
  await guest.getByPlaceholder('What needs doing?').fill(title)
  await guest.getByRole('button', { name: 'Add' }).click()
  await expect(owner.getByText(title)).toBeVisible()

  // Back in the owner's own organisation, the team's todo isn't there.
  const personal = owner.getByLabel('Organisation', { exact: true })
  const ownOrganisation = await personal.locator('option').filter({ hasNotText: team }).first().getAttribute('value')
  await personal.selectOption(ownOrganisation!)
  await expect(owner.getByText('Loading…')).toBeHidden()
  await expect(owner.getByText(title)).toHaveCount(0)
})

test('an admin finds an organisation in the back office and gives it a plan', async ({ browser }) => {
  const owner = await (await browser.newContext()).newPage()
  const email = await signUp(owner)

  const admin = await (await browser.newContext()).newPage()
  await logInAsAdmin(admin)
  await openFromNav(admin, 'Admin')
  await expect(admin.getByText('Monthly recurring revenue')).toBeVisible()
  await admin.getByRole('navigation', { name: 'Admin' }).getByRole('link', { name: 'Organisations' }).click()
  // A new account's organisation is named after its email.
  await admin.getByPlaceholder('Search by name').fill(email)
  await admin.getByRole('link', { name: email }).click()
  await expect(admin.getByRole('heading', { name: email })).toBeVisible()

  await admin.getByRole('button', { name: 'Change the trial' }).click()
  await expect(admin.getByText(/Done: Pro until/)).toBeVisible()
  await expect(admin.getByRole('cell', { name: 'organisation.grant-plan' })).toBeVisible()
})
