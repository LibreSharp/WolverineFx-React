import { useState } from 'react'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ErrorText } from '../../components/Messages'
import { Section } from '../../components/Section'
import { useAuditLog } from './adminQueries'
import { Pager } from './Pager'

// /admin/audit: every admin action, newest first. targetId: only those about one user,
// organisation or feature (as on an organisation's page).
export function AuditLog({ targetId, title = 'Audit log' }: { targetId?: string; title?: string }) {
  const [page, setPage] = useState(1)
  const audit = useAuditLog(page, targetId)
  return (
    <Section title={title} description={targetId ? undefined : 'Who changed what in the back office, and when.'}>
      <ErrorText error={audit.error} />
      {audit.data && (
        <>
          {audit.data.entries.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nothing yet.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>When</TableHead>
                  <TableHead>Who</TableHead>
                  <TableHead>What</TableHead>
                  <TableHead>Details</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {audit.data.entries.map((entry) => (
                  <TableRow key={entry.id}>
                    <TableCell className="whitespace-nowrap">{entry.at && new Date(entry.at).toLocaleString()}</TableCell>
                    <TableCell>{entry.actorEmail}</TableCell>
                    <TableCell className="font-mono text-xs">{entry.action}</TableCell>
                    <TableCell className="whitespace-normal">{entry.details}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
          <Pager total={audit.data.total} noun="entry" page={page} pageSize={audit.data.pageSize} onChange={setPage} />
        </>
      )}
    </Section>
  )
}

export function AuditLogPage() {
  return <AuditLog />
}
