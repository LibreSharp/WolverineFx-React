import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

// A feature flag override: on, off, or the default (no override). defaultLabel: what the default
// is, such as "on" or "as the plans say".
export function OverrideSelect({
  label,
  value,
  defaultLabel,
  disabled,
  onChange,
}: {
  label: string
  value: boolean | null | undefined
  defaultLabel: string
  disabled?: boolean
  onChange: (enabled: boolean | null) => void
}) {
  return (
    <NativeSelect
      size="sm"
      aria-label={label}
      disabled={disabled}
      value={value === true ? 'on' : value === false ? 'off' : ''}
      onChange={(event) => onChange(event.target.value === 'on' ? true : event.target.value === 'off' ? false : null)}
    >
      <NativeSelectOption value="">Default ({defaultLabel})</NativeSelectOption>
      <NativeSelectOption value="on">On</NativeSelectOption>
      <NativeSelectOption value="off">Off</NativeSelectOption>
    </NativeSelect>
  )
}
