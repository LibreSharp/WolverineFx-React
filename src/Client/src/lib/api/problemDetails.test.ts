import { describe, expect, it } from 'vitest'
import { problemMessage } from './problemDetails'

describe('problemMessage', () => {
  it('joins validation messages from every field', () => {
    const error = { title: 'One or more validation errors occurred.', errors: { Title: ['Required.'], Email: ['Invalid.'] } }
    expect(problemMessage(error, 'fallback')).toBe('Required. Invalid.')
  })

  it('uses the title when there are no field errors', () => {
    expect(problemMessage({ title: 'Registration is closed.' }, 'fallback')).toBe('Registration is closed.')
  })

  it('falls back when the body is not ProblemDetails', () => {
    expect(problemMessage(undefined, 'fallback')).toBe('fallback')
    expect(problemMessage('oops', 'fallback')).toBe('fallback')
  })
})
