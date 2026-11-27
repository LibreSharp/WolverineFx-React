import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useAuthOptions } from '../auth/authQueries'
import { isCancelled, passkeysSupported } from '../../lib/passkeys'
import { useNavigate } from 'react-router'
import { setTokens } from '../../lib/api/authTokens'
import type { HomeLocationState } from '../../components/HomePage'
import {
  startLinkingExternalLogin,
  useChangePassword,
  useDeleteAccount,
  useAddPasskey,
  useExternalLogins,
  useMe,
  usePasskeys,
  useRemoveExternalLogin,
  useRemovePasskey,
  useRenamePasskey,
  useUpdateProfile,
} from './accountQueries'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Section } from '../../components/Section'
import { DownloadDataSection } from '../notifications/DownloadDataSection'
import { EmailPreferencesSection } from '../notifications/EmailPreferencesSection'

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block text-muted-foreground">{label}</span>
      {children}
    </label>
  )
}

function Result({ error, success }: { error: Error | null; success?: string | false }) {
  if (error) return <p role="alert" className="text-sm text-destructive">{error.message}</p>
  if (success) return <p role="status" className="text-sm text-emerald-700 dark:text-emerald-300">{success}</p>
  return null
}

function ProfileSection() {
  const me = useMe()
  const update = useUpdateProfile()
  const [email, setEmail] = useState<string | null>(null)
  const [displayName, setDisplayName] = useState<string | null>(null)

  if (me.isPending) return <Section title="Profile"><p className="text-muted-foreground">Loading…</p></Section>
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
          <Input type="email" required value={emailValue} onChange={(e) => setEmail(e.target.value)} />
        </Field>
        <Field label="Display name (optional)">
          <Input value={displayNameValue} maxLength={100} onChange={(e) => setDisplayName(e.target.value)} />
        </Field>
        <Result error={update.error} success={update.isSuccess && saved} />
        <Button type="submit" disabled={update.isPending}>
          Save profile
        </Button>
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
          <Input type="password" required autoComplete="current-password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} />
        </Field>
        <Field label="New password">
          <Input type="password" required autoComplete="new-password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} />
        </Field>
        <Field label="Confirm new password">
          <Input type="password" required autoComplete="new-password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} />
        </Field>
        {mismatch && <p role="alert" className="text-sm text-destructive">The new passwords don't match.</p>}
        <Result error={change.error} success={change.isSuccess && 'Password changed. Your other sessions have been signed out.'} />
        <Button type="submit" disabled={change.isPending || mismatch}>
          Change password
        </Button>
      </form>
    </Section>
  )
}

function PasskeyRow({ passkey }: { passkey: { id: string; name: string; createdAt: string } }) {
  const rename = useRenamePasskey()
  const remove = useRemovePasskey()
  const [name, setName] = useState<string | null>(null)
  const error = rename.error ?? remove.error

  return (
    <li className="text-sm">
      <div className="flex items-center gap-2">
        {name === null ? (
          <span className="flex-1">
            {passkey.name}{' '}
            <span className="text-muted-foreground">· added {new Date(passkey.createdAt).toLocaleDateString()}</span>
          </span>
        ) : (
          <Input value={name} maxLength={100} onChange={(e) => setName(e.target.value)} className="flex-1" />
        )}
        {name === null ? (
          <Button variant="outline" type="button" onClick={() => setName(passkey.name)}>
            Rename
          </Button>
        ) : (
          <Button variant="outline"
            type="button"
            disabled={!name.trim() || rename.isPending}
            onClick={() => rename.mutate({ id: passkey.id, name }, { onSuccess: () => setName(null) })}
          >
            Save
          </Button>
        )}
        <Button variant="outline"
          type="button"
          disabled={remove.isPending}
          onClick={() => remove.mutate(passkey.id)}
        >
          Remove
        </Button>
      </div>
      {error && <p role="alert" className="mt-1 text-destructive">{error.message}</p>}
    </li>
  )
}

function PasskeysSection() {
  const passkeys = usePasskeys()
  const add = useAddPasskey()
  const [name, setName] = useState('')

  if (!passkeysSupported()) return null

  function submit(event: FormEvent) {
    event.preventDefault()
    add.mutate(name, { onSuccess: () => setName('') })
  }

  return (
    <Section title="Passkeys">
      <p className="mb-4 text-sm text-muted-foreground">
        Log in with your fingerprint, face or device PIN instead of a password. A passkey stays on your device (or in
        your password manager) and works only on this site.
      </p>
      <ul className="mb-4 space-y-2">
        {passkeys.data?.map((passkey) => <PasskeyRow key={passkey.id} passkey={passkey} />)}
        {passkeys.data?.length === 0 && <li className="text-sm text-muted-foreground">No passkeys yet.</li>}
      </ul>
      <form onSubmit={submit} className="flex gap-2">
        <Input
          placeholder="Name, e.g. Laptop"
          value={name}
          maxLength={100}
          onChange={(e) => setName(e.target.value)}
          className="flex-1"
        />
        <Button type="submit" disabled={add.isPending}>
          Add a passkey
        </Button>
      </form>
      <div className="mt-3">
        <Result error={add.error && !isCancelled(add.error) ? add.error : null} success={add.isSuccess && 'Passkey added.'} />
      </div>
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
            <Button variant="outline"
              type="button"
              disabled={remove.isPending}
              onClick={() => remove.mutate(login.provider)}
            >
              Remove
            </Button>
          </li>
        ))}
        {linked.length === 0 && <li className="text-sm text-muted-foreground">No providers linked.</li>}
      </ul>
      <div className="flex flex-wrap gap-2">
        {unlinked.map((provider) => (
          <Button variant="outline" size="sm"
            key={provider.name}
            type="button"
            onClick={() => startLinkingExternalLogin(provider.name).catch((e: Error) => setLinkError(e.message))}
          >
            Link {provider.displayName}
          </Button>
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
      <p className="mb-4 text-sm text-muted-foreground">
        This permanently deletes your account and all your todos. It can't be undone.
      </p>
      <form onSubmit={submit} className="space-y-4">
        <Field label="Enter your password to confirm">
          <Input type="password" required autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
        </Field>
        <Result error={deleteAccount.error} />
        <Button variant="destructive"
          type="submit"
          disabled={deleteAccount.isPending}
        >
          Delete my account
        </Button>
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
      <PasskeysSection />
      <SignInMethodsSection />
      <EmailPreferencesSection />
      <DownloadDataSection />
      <DeleteAccountSection />
    </div>
  )
}
