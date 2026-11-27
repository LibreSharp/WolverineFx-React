import { useState } from 'react'
import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ErrorText } from '../../components/Messages'
import { Section } from '../../components/Section'
import { useAdminOrganisations } from './backOfficeQueries'
import { Pager } from './Pager'

// /admin/organisations: every organisation, by name, with its plan.
export function OrganisationsPage() {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const organisations = useAdminOrganisations(search, page)

  return (
    <Section title="Organisations">
      <Input
        type="search"
        placeholder="Search by name"
        value={search}
        onChange={(event) => {
          setSearch(event.target.value)
          setPage(1)
        }}
        className="mb-2"
      />
      <ErrorText error={organisations.error} />
      {organisations.data && (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Plan</TableHead>
                <TableHead className="text-right">Members</TableHead>
                <TableHead>Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {organisations.data.organisations.map((organisation) => (
                <TableRow key={organisation.id}>
                  <TableCell>
                    <Link to={`/admin/organisations/${organisation.id}`} className="font-medium text-primary hover:underline">
                      {organisation.name}
                    </Link>
                    {organisation.suspended && (
                      <Badge variant="destructive" className="ml-2">
                        Suspended
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    {organisation.planName} <span className="text-muted-foreground">· {organisation.status}</span>
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{organisation.members}</TableCell>
                  <TableCell>{new Date(organisation.createdAt).toLocaleDateString()}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {organisations.data.organisations.length === 0 && <p className="py-3 text-sm text-muted-foreground">No organisations found.</p>}
          <Pager total={organisations.data.total} noun="organisation" page={page} pageSize={organisations.data.pageSize} onChange={setPage} />
        </>
      )}
    </Section>
  )
}
