import { api } from './api/client'
import { problemMessage } from './api/problemDetails'

// Passkeys with the browser's WebAuthn API. The API produces the options (Identity's
// IPasskeyHandler) and checks the result; these helpers pass them between the API and the browser.

// Older browsers lack the JSON helpers used here; the passkey buttons are hidden there.
export function passkeysSupported(): boolean {
  return (
    typeof window !== 'undefined' &&
    'PublicKeyCredential' in window &&
    typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function' &&
    typeof PublicKeyCredential.parseRequestOptionsFromJSON === 'function'
  )
}

// The person closed the browser's passkey dialog: not an error worth showing.
export function isCancelled(error: unknown): boolean {
  return error instanceof DOMException && (error.name === 'NotAllowedError' || error.name === 'AbortError')
}

// Logs in with a passkey the browser offers (no email needed). Returns the API's tokens.
export async function loginWithPasskey() {
  const { data: options } = await api.POST('/api/auth/passkey/options')
  if (!options) throw new Error('Could not start a passkey login.')

  const credential = (await navigator.credentials.get({
    publicKey: PublicKeyCredential.parseRequestOptionsFromJSON(JSON.parse(options.optionsJson)),
  })) as PublicKeyCredential | null
  if (!credential) throw new DOMException('No passkey chosen', 'NotAllowedError')

  const { data, error } = await api.POST('/api/auth/passkey/login', {
    body: { credentialJson: JSON.stringify(credential.toJSON()), state: options.state },
  })
  if (!data) throw new Error(problemMessage(error, "That passkey didn't work for this site."))
  return data
}

// Creates a passkey for the signed-in user and saves it with the given name.
export async function addPasskey(name: string) {
  const { data: options } = await api.POST('/api/account/passkeys/options')
  if (!options) throw new Error('Could not start adding a passkey.')

  const credential = (await navigator.credentials.create({
    publicKey: PublicKeyCredential.parseCreationOptionsFromJSON(JSON.parse(options.optionsJson)),
  })) as PublicKeyCredential | null
  if (!credential) throw new DOMException('No passkey created', 'NotAllowedError')

  const { error, response } = await api.POST('/api/account/passkeys', {
    body: { credentialJson: JSON.stringify(credential.toJSON()), state: options.state, name: name || null },
  })
  if (!response.ok) throw new Error(problemMessage(error, 'Could not add the passkey.'))
}
