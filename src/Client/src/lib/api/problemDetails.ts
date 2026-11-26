// Turns an API error body into a message for the user. Validation failures come back as
// ProblemDetails with an `errors` map (field -> messages); other errors may only have a title.
export function problemMessage(error: unknown, fallback: string): string {
  if (error && typeof error === 'object') {
    const { errors, title } = error as { errors?: Record<string, string[]>; title?: string | null }
    const messages = Object.values(errors ?? {}).flat()
    if (messages.length > 0) return messages.join(' ')
    if (title) return title
  }
  return fallback
}
