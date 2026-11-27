import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { ErrorText } from '../../components/Messages'
import { Section } from '../../components/Section'
import { useEmailPreferences, useUpdateEmailPreferences } from './notificationQueries'

// Settings: which kinds of notification are also emailed. Every notification email has a link to
// turn its kind off, too. Account emails (confirmation, password reset) can't be turned off.
export function EmailPreferencesSection() {
  const preferences = useEmailPreferences()
  const update = useUpdateEmailPreferences()

  return (
    <Section title="Email notifications" description="Notifications always appear under the bell in the header. Choose which are emailed too.">
      {preferences.isPending && <p className="text-muted-foreground">Loading…</p>}
      <ErrorText error={preferences.error ?? update.error} />
      <ul className="space-y-3">
        {preferences.data?.categories.map((c) => (
          <li key={c.category} className="flex items-center gap-3">
            <Checkbox
              id={`email-${c.category}`}
              checked={c.email}
              disabled={update.isPending}
              onCheckedChange={(checked) => update.mutate({ category: c.category, email: checked === true })}
            />
            <Label htmlFor={`email-${c.category}`}>{c.description}</Label>
          </li>
        ))}
      </ul>
    </Section>
  )
}
