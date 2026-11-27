import { expect, test } from '@playwright/test'

// Without accounts: the Todo page works and updates every open tab.

test('the home page renders', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: /Welcome to/ })).toBeVisible()
})

test('todos are added, completed, and appear in other tabs as they change', async ({ page, context }) => {
  const title = `Buy milk ${Date.now()}`
  const other = await context.newPage()
  await other.goto('/todos')
  await page.goto('/todos')

  await page.getByPlaceholder('What needs doing?').fill(title)
  await page.getByRole('button', { name: 'Add' }).click()
  await expect(page.getByText(title)).toBeVisible()

  // The other tab updates over SignalR, without reloading.
  await expect(other.getByText(title)).toBeVisible()

  await page.getByRole('listitem').filter({ hasText: title }).getByRole('button', { name: 'Complete' }).click()
  await expect(other.getByText(title)).toHaveClass(/line-through/)
})
