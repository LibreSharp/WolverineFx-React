import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useAuthOptions } from '../auth/authQueries'
import { useNavigate } from 'react-router'
import { setTokens } from '../../lib/api/authTokens'
import type { HomeLocationState } from '../../components/HomePage'
import {
  startLinkingExternalLogin,
  useChangePassword,
  useDeleteAccount,
  useExternalLogins,
  useMe,
  useRemoveExternalLogin,
  useUpdateProfile,
} from './accountQueries'

const inputClass = 'w-full rounded border border-gray-300 px-3 py-2'
const buttonClass = 'rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50'

function Section({ title, children, tone }: { title: string; children: ReactNode; tone?: 'danger' }) {
  return (
    <section
      className={`rounded border bg-white p-6 ${tone === 'danger' ? 'border-red-200' : 'border-gray-200'}`}
    >
      <h2 className="mb-4 text-lg font-semibold">{title}</h2>
      {children}
    </section>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block text-gray-700">{label}</span>
      {children}
    </label>
  )
}

function Result({ error, success }: { error: Error | null; success?: string | false }) {
  if (error) return <p role="alert" className="text-sm text-red-600">{error.message}</p>
  if (success) return <p role="status" className="text-sm text-green-700">{success}</p>
  return null
}

function ProfileSection() {
  const me = useMe()
  const update = useUpdateProfile()
  const [email, setEmail] = useState<string | null>(null)
  const [displayName, setDisplayName] = useState<string | null>(null)

  if (me.isPending) return <Section title="Profile"><p className="text-gray-500">Loading…</p></Section>
  if (me.isError) return <Section title="Profile"><Result error={me.error} /></Section>

  // Show what the user typed, or the saved values until they type.
  const emailValue = email ?? me.data.email
  const displayNameValue = displayName ?? me.data.displayName ?? ''

  function save(event: FormEvent) {
    event.preventDefault()
    // Back to the saved email: a new one only applies once it's confirmed.
    update.mutate({ email: emailValue, displayName: displayNameValue || null }, { onSuccess: () => setEmail(null) })
  }

  const sentTo = update.data?.confirmationSentTo
  const saved = sentTo
    ? `Profile saved. To change your email, open the link we've sent to ${sentTo}.`
    : 'Profile saved.'


  return (
    <Section title="Profile">
      <form onSubmit={save} className="space-y-4">
        <Field label="Email (you log in with this)">
          <input type="email" required value={emailValue} onChange={(e) => setEmail(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Display name (optional)">
          <input value={displayNameValue} maxLength={100} onChange={(e) => setDisplayName(e.target.value)} className={inputClass} />
        </Field>
        <Result error={update.error} success={update.isSuccess && saved} />
        <button type="submit" disabled={update.isPending} className={buttonClass}>
          Save profile
        </button>
      </form>
    </Section>
  )
}

function PasswordSection() {
  const change = useChangePassword()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const mismatch = confirmPassword.length > 0 && confirmPassword !== newPassword

  function submit(event: FormEvent) {
    event.preventDefault()
    if (mismatch) return
    change.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: () => {
          setCurrentPassword('')
          setNewPassword('')
          setConfirmPassword('')
        },
      },
    )
  }

  return (
    <Section title="Change password">
      <form onSubmit={submit} className="space-y-4">
        <Field label="Current password">
          <input type="password" required autoComplete="current-password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} className={inputClass} />
        </Field>
        <Field label="New password">
          <input type="password" required autoComplete="new-password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Confirm new password">
          <input type="password" required autoComplete="new-password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} className={inputClass} />
        </Field>
        {mismatch && <p role="alert" className="text-sm text-red-600">The new passwords don't match.</p>}
        <Result error={change.error} success={change.isSuccess && 'Password changed. Your other sessions have been signed out.'} />
        <button type="submit" disabled={change.isPending || mismatch} className={buttonClass}>
          Change password
        </button>
      </form>
    </Section>
  )
}

// The API sends the browser back here after linking a provider, with the result in the URL
// fragment (#linked=Google or #error=...).
function readLinkResult(): { linked?: string; error?: string } {
  const fragment = new URLSearchParams(window.location.hash.slice(1))
  return { linked: fragment.get('linked') ?? undefined, error: fragment.get('error') ?? undefined }
}

function SignInMethodsSection() {
  const options = useAuthOptions()
  const logins = useExternalLogins()
  const remove = useRemoveExternalLogin()
  const [linkResult] = useState(readLinkResult)
  // Then remove it from the address bar, so a reload doesn't show it again.
  useEffect(() => {
    if (window.location.hash) window.history.replaceState(null, '', window.location.pathname)
  }, [])
  const [linkError, setLinkError] = useState<string | null>(null)
  const providers = options.data?.externalProviders ?? []

  if (providers.length === 0 && (logins.data?.length ?? 0) === 0) return null

  const linked = logins.data ?? []
  const unlinked = providers.filter((p) => !linked.some((l) => l.provider === p.name))

  return (
    <Section title="Sign-in methods">
      <ul className="mb-4 space-y-2">
        {linked.map((login) => (
          <li key={login.provider} className="flex items-center justify-between text-sm">
            <span>{login.displayName}</span>
            <button
              type="button"
              disabled={remove.isPending}
              onClick={() => remove.mutate(login.provider)}
              className="rounded border border-gray-300 px-2 py-1 hover:bg-gray-100 disabled:opacity-50"
            >
              Remove
            </button>
          </li>
        ))}
        {linked.length === 0 && <li className="text-sm text-gray-500">No providers linked.</li>}
      </ul>
      <div className="flex flex-wrap gap-2">
        {unlinked.map((provider) => (
          <button
            key={provider.name}
            type="button"
            onClick={() => startLinkingExternalLogin(provider.name).catch((e: Error) => setLinkError(e.message))}
            className="rounded border border-gray-300 px-3 py-2 text-sm hover:bg-gray-100"
          >
            Link {provider.displayName}
          </button>
        ))}
      </div>
      <div className="mt-3">
        <Result
          error={remove.error ?? (linkError || linkResult.error ? new Error(linkError ?? linkResult.error) : null)}
          success={linkResult.linked && `${linkResult.linked} is now linked: you can use it to log in.`}
        />
      </div>
    </Section>
  )
}

function DeleteAccountSection() {
  const deleteAccount = useDeleteAccount()
  const navigate = useNavigate()
  const [password, setPassword] = useState('')

  function submit(event: FormEvent) {
    event.preventDefault()
    deleteAccount.mutate(password, {
      onSuccess: () => {
        // Leave the protected page before signing out, so the redirect goes home, not to /login.
        navigate('/', { replace: true, state: { notice: 'Your account has been deleted.' } satisfies HomeLocationState })
        setTokens(null)
      },
    })
  }

  return (
    <Section title="Delete account" tone="danger">
      <p className="mb-4 text-sm text-gray-600">
        This permanently deletes your account and all your todos. It can't be undone.
      </p>
      <form onSubmit={submit} className="space-y-4">
        <Field label="Enter your password to confirm">
          <input type="password" required autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} className={inputClass} />
        </Field>
        <Result error={deleteAccount.error} />
        <button
          type="submit"
          disabled={deleteAccount.isPending}
          className="rounded bg-red-600 px-4 py-2 text-white hover:bg-red-700 disabled:opacity-50"
        >
          Delete my account
        </button>
      </form>
    </Section>
  )
}

export function SettingsPage() {
  return (
    <div className="mx-auto max-w-xl space-y-6 px-6 py-10">
      <h1 className="text-2xl font-semibold">Settings</h1>
      <ProfileSection />
      <PasswordSection />
      <SignInMethodsSection />
      <DeleteAccountSection />
    </div>
  )
}
