import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { setTokens } from '../lib/api/authTokens'
import { server } from './server'

// A request no test expected fails the test, so nothing reaches a real server by accident.
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))
afterEach(() => {
  cleanup()
  server.resetHandlers()
  setTokens(null)
  window.history.replaceState(null, '', '/')
})
afterAll(() => server.close())
