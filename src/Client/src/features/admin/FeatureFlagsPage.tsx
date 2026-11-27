import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ErrorText } from '../../components/Messages'
import { Section } from '../../components/Section'
import { useFeatures, useSetFeature } from './backOfficeQueries'
import { OverrideSelect } from './OverrideSelect'

// /admin/features: turn a feature on or off for everyone or for a plan, over what the plan
// catalogue says. An organisation's own override (on its page) wins over both, and everyone's
// wins over a plan's.
export function FeatureFlagsPage() {
  const features = useFeatures()
  const setFeature = useSetFeature()
  if (features.isPending) return <p className="text-muted-foreground">Loading…</p>
  if (features.isError) return <ErrorText error={features.error} />

  const plans = features.data[0]?.plans ?? []
  return (
    <Section
      title="Feature flags"
      description="The default is what the plans include. An organisation's own setting, on its page, wins over these."
    >
      <ErrorText error={setFeature.error} className="mb-2" />
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Feature</TableHead>
            <TableHead>Everyone</TableHead>
            {plans.map((plan) => (
              <TableHead key={plan.planId}>{plan.planName}</TableHead>
            ))}
            <TableHead className="text-right">Organisations</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {features.data.map((feature) => (
            <TableRow key={feature.name}>
              <TableCell className="font-medium">{feature.description}</TableCell>
              <TableCell>
                <OverrideSelect
                  label={`${feature.description} for everyone`}
                  value={feature.global}
                  defaultLabel="as the plans say"
                  disabled={setFeature.isPending}
                  onChange={(enabled) => setFeature.mutate({ feature: feature.name, scope: 'Global', enabled })}
                />
              </TableCell>
              {feature.plans.map((plan) => (
                <TableCell key={plan.planId}>
                  <OverrideSelect
                    label={`${feature.description} on ${plan.planName}`}
                    value={plan.override}
                    defaultLabel={plan.included ? 'on' : 'off'}
                    disabled={setFeature.isPending}
                    onChange={(enabled) => setFeature.mutate({ feature: feature.name, scope: 'Plan', scopeKey: plan.planId, enabled })}
                  />
                </TableCell>
              ))}
              <TableCell className="text-right tabular-nums">{feature.organisationOverrides}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Section>
  )
}
