import { defineConfig } from '@playwright/test'

// End-to-end tests: a real browser against the running app. They don't start the app: run it
// first (see README.md, "Tests"), then `npm test` here. Set E2E_BASE_URL and MAILPIT_URL if the
// app or Mailpit aren't at the defaults below.
export default defineConfig({
  testDir: './specs',
  // One at a time: the tests share one app and database.
  workers: 1,
  fullyParallel: false,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? 'list' : 'html',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5000',
    // The Chrome installed on the machine, so there's no browser to download.
    channel: 'chrome',
    trace: 'retain-on-failure',
  },
})
