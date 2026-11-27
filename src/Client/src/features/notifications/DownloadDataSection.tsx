import { useMutation } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { ErrorText } from '../../components/Messages'
import { Section } from '../../components/Section'
import { downloadFile } from '../../lib/download'

// Settings: "Download your data", everything the app stores about the user as a JSON file.
export function DownloadDataSection() {
  const download = useMutation({ mutationFn: () => downloadFile('/api/account/export', 'my-data.json') })
  return (
    <Section title="Your data" description="Download everything this app stores about you, as a JSON file.">
      <ErrorText error={download.error} className="mb-3" />
      <Button variant="outline" disabled={download.isPending} onClick={() => download.mutate()}>
        Download your data
      </Button>
    </Section>
  )
}
