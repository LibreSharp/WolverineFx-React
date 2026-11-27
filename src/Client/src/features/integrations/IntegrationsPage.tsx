import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { ErrorText, Notice } from '../../components/Messages'
import { Section } from '../../components/Section'
import { canManageMembers, useCurrentOrganisation } from '../organisations/organisationQueries'
import {
  scopes,
  useApiKeys,
  useCreateApiKey,
  useCreateWebhook,
  useDeleteSecret,
  useDeleteWebhook,
  useDeliveries,
  useResendDelivery,
  useRevokeApiKey,
  useRotateApiKey,
  useSecrets,
  useSendTestEvent,
  useSetSecret,
  useUpdateWebhook,
  useWebhookEvents,
  useWebhooks,
  useWebhookSecret,
  type Scope,
} from './integrationQueries'

const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' })
const timeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

// /organisation/integrations: how other programs work with the organisation's data. Owners and
// admins manage API keys and webhooks; owners manage secrets. The API enforces who may do what.
export function IntegrationsPage() {
  const organisation = useCurrentOrganisation()
  if (organisation.isPending) return <p className="mx-auto max-w-3xl px-6 py-10 text-muted-foreground">Loading…</p>
  if (organisation.isError) return <div className="mx-auto max-w-3xl px-6 py-10"><ErrorText error={organisation.error} /></div>

  const role = organisation.data.role
  return (
    <div key={organisation.data.id} className="mx-auto max-w-3xl space-y-6 px-6 py-10">
      <div>
        <Link to="/organisation" className="text-sm text-muted-foreground hover:text-foreground">← {organisation.data.name}</Link>
        <h1 className="mt-1 text-2xl font-semibold">Integrations</h1>
      </div>
      {canManageMembers(role) ? (
        <>
          <ApiKeysSection />
          <McpSection />
          <WebhooksSection />
          {role === 'Owner' && <SecretsSection />}
        </>
      ) : (
        <Notice tone="warning">Only owners and admins can manage integrations.</Notice>
      )}
    </div>
  )
}

// A key or secret that's shown once: with a copy button, and a warning to keep it now.
function ShownOnce({ label, value }: { label: string; value: string }) {
  const [copied, setCopied] = useState(false)
  return (
    <div className="mt-4 space-y-2 rounded-md border bg-muted p-3 text-sm">
      <p className="font-medium">{label}</p>
      <div className="flex gap-2">
        <code className="flex-1 overflow-x-auto rounded bg-background px-2 py-1" aria-label={label}>{value}</code>
        <Button
          size="sm"
          variant="outline"
          onClick={() => void navigator.clipboard?.writeText(value).then(() => setCopied(true))}
        >
          {copied ? 'Copied' : 'Copy'}
        </Button>
      </div>
    </div>
  )
}

function ApiKeysSection() {
  const keys = useApiKeys()
  const create = useCreateApiKey()
  const rotate = useRotateApiKey()
  const revoke = useRevokeApiKey()
  const [name, setName] = useState('')
  const [scope, setScope] = useState<Scope>('read')
  const [expires, setExpires] = useState('')
  const issued = create.data ?? rotate.data

  function submit(event: FormEvent) {
    event.preventDefault()
    rotate.reset()
    create.mutate({ name, scope, expiresInDays: expires ? Number(expires) : null }, { onSuccess: () => setName('') })
  }

  return (
    <Section
      title="API keys"
      description="For scripts and other services: send a key as “Authorization: Bearer sk_…”. A key acts as you in this organisation, and can only reach the Todos API and the MCP endpoint."
    >
      <form onSubmit={submit} className="flex flex-wrap gap-2">
        <Label htmlFor="key-name" className="sr-only">Key name</Label>
        <Input id="key-name" required maxLength={100} placeholder="Name, such as “Deploy script”" value={name} onChange={(e) => setName(e.target.value)} className="min-w-48 flex-1" />
        <Label htmlFor="key-scope" className="sr-only">Scope</Label>
        <NativeSelect id="key-scope" value={scope} onChange={(e) => setScope(e.target.value as Scope)}>
          {scopes.map((s) => <NativeSelectOption key={s} value={s}>{s === 'read' ? 'Read only' : 'Read and write'}</NativeSelectOption>)}
        </NativeSelect>
        <Label htmlFor="key-expires" className="sr-only">Expires</Label>
        <NativeSelect id="key-expires" value={expires} onChange={(e) => setExpires(e.target.value)}>
          <NativeSelectOption value="">Never expires</NativeSelectOption>
          <NativeSelectOption value="30">Expires in 30 days</NativeSelectOption>
          <NativeSelectOption value="90">Expires in 90 days</NativeSelectOption>
          <NativeSelectOption value="365">Expires in a year</NativeSelectOption>
        </NativeSelect>
        <Button type="submit" disabled={create.isPending}>Create key</Button>
      </form>
      <ErrorText error={create.error ?? rotate.error ?? revoke.error ?? keys.error} className="mt-2" />
      {issued && <ShownOnce label={`Your new key “${issued.apiKey.name}”: copy it now, it won't be shown again.`} value={issued.key} />}
      {keys.data && keys.data.length > 0 && (
        <ul className="mt-4 divide-y divide-border">
          {keys.data.map((key) => (
            <li key={key.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
              <span>
                <span className="font-medium">{key.name}</span> <code className="text-muted-foreground">{key.hint}…</code>
                <span className="block text-xs text-muted-foreground">
                  {key.scope === 'read' ? 'Read only' : 'Read and write'} ·{' '}
                  {key.lastUsedAt ? `last used ${timeFormat.format(new Date(key.lastUsedAt))}` : 'never used'}
                  {key.expiresAt && ` · ${key.expired ? 'expired' : 'expires'} ${dateFormat.format(new Date(key.expiresAt))}`}
                </span>
              </span>
              <span className="flex gap-3">
                {!key.expired && (
                  <Button variant="link" size="sm" className="h-auto px-0" onClick={() => { create.reset(); rotate.mutate(key.id) }}>
                    Rotate
                  </Button>
                )}
                <Button variant="link" size="sm" className="h-auto px-0 text-destructive" onClick={() => revoke.mutate(key.id)}>
                  Revoke
                </Button>
              </span>
            </li>
          ))}
        </ul>
      )}
      <p className="mt-4 text-xs text-muted-foreground">Rotating makes a new key; the old one keeps working for a day, so programs can switch over.</p>
    </Section>
  )
}

function McpSection() {
  const url = `${typeof window === 'undefined' ? '' : window.location.origin}/mcp`
  const config = JSON.stringify({ mcpServers: { todos: { type: 'http', url, headers: { Authorization: 'Bearer sk_…' } } } }, null, 2)
  return (
    <Section
      title="AI assistants (MCP)"
      description="Assistants that support the Model Context Protocol can list, add and complete this organisation's Todos. Give them an API key: a read-only key lets them look but not change anything."
    >
      <p className="mb-2 text-sm">
        Server address: <code className="rounded bg-muted px-1">{url}</code>
      </p>
      <pre className="overflow-x-auto rounded-md bg-muted p-3 text-xs">{config}</pre>
    </Section>
  )
}

function WebhooksSection() {
  const webhooks = useWebhooks()
  const events = useWebhookEvents()
  const create = useCreateWebhook()
  const [url, setUrl] = useState('')
  const [description, setDescription] = useState('')
  const [chosen, setChosen] = useState<string[]>([])

  function submit(event: FormEvent) {
    event.preventDefault()
    create.mutate({ url, description, events: chosen, enabled: true }, { onSuccess: () => { setUrl(''); setDescription(''); setChosen([]) } })
  }

  return (
    <Section
      title="Webhooks"
      description="This app sends an HTTP POST to your URL when something happens, signed the Standard Webhooks way so you can check it came from here. Failed deliveries are retried for about 10 hours."
    >
      <form onSubmit={submit} className="space-y-3">
        <div className="flex flex-wrap gap-2">
          <Label htmlFor="webhook-url" className="sr-only">Webhook URL</Label>
          <Input id="webhook-url" type="url" required placeholder="https://example.com/webhooks" value={url} onChange={(e) => setUrl(e.target.value)} className="min-w-64 flex-1" />
          <Label htmlFor="webhook-description" className="sr-only">Description</Label>
          <Input id="webhook-description" maxLength={200} placeholder="Description (optional)" value={description} onChange={(e) => setDescription(e.target.value)} className="flex-1" />
        </div>
        <fieldset className="flex flex-wrap gap-4 text-sm">
          <legend className="mb-1 text-muted-foreground">Events (none ticked: all of them)</legend>
          {events.data?.map((e) => (
            <span key={e.type} className="flex items-center gap-2">
              <Checkbox
                id={`event-${e.type}`}
                checked={chosen.includes(e.type)}
                onCheckedChange={(checked) => setChosen((current) => (checked === true ? [...current, e.type] : current.filter((t) => t !== e.type)))}
              />
              <Label htmlFor={`event-${e.type}`}>{e.description} <code className="text-xs text-muted-foreground">{e.type}</code></Label>
            </span>
          ))}
        </fieldset>
        <Button type="submit" disabled={create.isPending}>Add webhook</Button>
      </form>
      <ErrorText error={create.error ?? webhooks.error} className="mt-2" />
      {create.data && <ShownOnce label="The webhook's signing secret, to check signatures with (you can show it again later):" value={create.data.secret} />}
      <ul className="mt-4 space-y-4">
        {webhooks.data?.map((webhook) => <WebhookItem key={webhook.id} webhook={webhook} />)}
      </ul>
    </Section>
  )
}

interface Webhook {
  id: string
  url: string
  description: string
  events: string[]
  enabled: boolean
}

function WebhookItem({ webhook }: { webhook: Webhook }) {
  const update = useUpdateWebhook()
  const remove = useDeleteWebhook()
  const secret = useWebhookSecret()
  const test = useSendTestEvent()
  const [showDeliveries, setShowDeliveries] = useState(false)

  return (
    <li className="rounded-md border p-3 text-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="min-w-0">
          <span className="block truncate font-medium">{webhook.url}</span>
          <span className="block text-xs text-muted-foreground">
            {webhook.description && `${webhook.description} · `}
            {webhook.events.length === 0 ? 'All events' : webhook.events.join(', ')}
          </span>
        </span>
        <span className="flex items-center gap-2">
          <Checkbox
            id={`enabled-${webhook.id}`}
            checked={webhook.enabled}
            onCheckedChange={(checked) => update.mutate({ ...webhook, enabled: checked === true })}
          />
          <Label htmlFor={`enabled-${webhook.id}`}>On</Label>
        </span>
      </div>
      <div className="mt-2 flex flex-wrap gap-3">
        <Button variant="link" size="sm" className="h-auto px-0" onClick={() => secret.mutate(webhook.id)}>Show secret</Button>
        <Button variant="link" size="sm" className="h-auto px-0" onClick={() => { test.mutate(webhook.id); setShowDeliveries(true) }}>Send test event</Button>
        <Button variant="link" size="sm" className="h-auto px-0" onClick={() => setShowDeliveries((s) => !s)}>
          {showDeliveries ? 'Hide deliveries' : 'Deliveries'}
        </Button>
        <Button variant="link" size="sm" className="h-auto px-0 text-destructive" onClick={() => remove.mutate(webhook.id)}>Delete</Button>
      </div>
      <ErrorText error={update.error ?? remove.error ?? secret.error ?? test.error} className="mt-2" />
      {secret.data && <ShownOnce label="Signing secret" value={secret.data} />}
      {showDeliveries && <Deliveries webhookId={webhook.id} />}
    </li>
  )
}

function Deliveries({ webhookId }: { webhookId: string }) {
  const deliveries = useDeliveries(webhookId)
  const resend = useResendDelivery(webhookId)

  // The API's numbers can arrive as strings (OpenAPI's integer format).
  const status = (d: { deliveredAt?: string | null; statusCode?: number | string | null; attempts: number | string }) => {
    const attempts = Number(d.attempts)
    if (d.deliveredAt) return `Delivered (${d.statusCode})`
    return attempts === 0 ? 'Sending…' : `Failed (${d.statusCode ?? 'no answer'}), ${attempts} ${attempts === 1 ? 'try' : 'tries'}`
  }

  return (
    <div className="mt-3">
      {deliveries.isPending && <p className="text-muted-foreground">Loading…</p>}
      <ErrorText error={deliveries.error ?? resend.error} />
      {deliveries.data?.length === 0 && <p className="text-muted-foreground">Nothing sent yet.</p>}
      <ul className="divide-y divide-border">
        {deliveries.data?.map((d) => (
          <li key={d.id} className="flex flex-wrap items-center justify-between gap-2 py-1.5">
            <span>
              <code>{d.eventType}</code> <span className="text-muted-foreground">{timeFormat.format(new Date(d.createdAt))}</span>
              <span className={d.deliveredAt ? 'block text-xs text-emerald-700 dark:text-emerald-300' : 'block text-xs text-destructive'}>{status(d)}</span>
            </span>
            <Button variant="link" size="sm" className="h-auto px-0" disabled={resend.isPending} onClick={() => resend.mutate(d.id)}>Resend</Button>
          </li>
        ))}
      </ul>
    </div>
  )
}

function SecretsSection() {
  const secrets = useSecrets(true)
  const set = useSetSecret()
  const remove = useDeleteSecret()
  const [name, setName] = useState('')
  const [value, setValue] = useState('')

  function submit(event: FormEvent) {
    event.preventDefault()
    set.mutate({ name, value }, { onSuccess: () => { setName(''); setValue('') } })
  }

  return (
    <Section
      title="Secrets"
      description="Keys and tokens for other services that this app uses on the organisation's behalf. They're stored encrypted and never shown again: setting one with the same name replaces it."
    >
      <form onSubmit={submit} className="flex flex-wrap gap-2">
        <Label htmlFor="secret-name" className="sr-only">Secret name</Label>
        <Input id="secret-name" required pattern="[a-z0-9][a-z0-9\-]{0,63}" title="Lower case letters, digits and dashes" placeholder="Name, such as openai-api-key" value={name} onChange={(e) => setName(e.target.value)} className="flex-1" />
        <Label htmlFor="secret-value" className="sr-only">Value</Label>
        <Input id="secret-value" type="password" required autoComplete="off" placeholder="Value" value={value} onChange={(e) => setValue(e.target.value)} className="flex-1" />
        <Button type="submit" disabled={set.isPending}>Save secret</Button>
      </form>
      <ErrorText error={set.error ?? remove.error ?? secrets.error} className="mt-2" />
      {set.isSuccess && <Notice className="mt-2">Secret saved.</Notice>}
      {secrets.data && secrets.data.length > 0 && (
        <ul className="mt-4 divide-y divide-border">
          {secrets.data.map((s) => (
            <li key={s.name} className="flex items-center justify-between py-2 text-sm">
              <span>
                <code>{s.name}</code> {s.hint && <span className="text-muted-foreground">…{s.hint}</span>}
                <span className="block text-xs text-muted-foreground">Set {timeFormat.format(new Date(s.updatedAt))}</span>
              </span>
              <Button variant="link" size="sm" className="h-auto px-0 text-destructive" onClick={() => remove.mutate(s.name)}>Delete</Button>
            </li>
          ))}
        </ul>
      )}
    </Section>
  )
}
