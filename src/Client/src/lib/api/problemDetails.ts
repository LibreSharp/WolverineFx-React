// Turns an API error body into a message for the user. Validation failures come back as
// ProblemDetails with an `errors` map (field -> messages); other errors may only have a title.
// A plan's limit or feature (402 Payment Required) explains itself in `detail`.
export function problemMessage(error: unknown, fallback: string): string {
  if (error && typeof error === 'object') {
    const { errors, title, detail, status } = error as {
      errors?: Record<string, string[]>
      title?: string | null
      detail?: string | null
      status?: number | string | null
    }
    const messages = Object.values(errors ?? {}).flat()
    if (messages.length > 0) return messages.join(' ')
    if (Number(status) === 402 && detail) return detail
    if (title) return title
  }
  return fallback
}
