import { Button } from '@/components/ui/button'

// "12 users · page 1 of 3", with Previous and Next.
export function Pager({
  total,
  noun,
  page,
  pageSize,
  onChange,
}: {
  total: number | string
  noun: string
  page: number
  pageSize: number | string
  onChange: (page: number) => void
}) {
  // The OpenAPI document allows numbers as strings, so convert them.
  const count = Number(total)
  const pageCount = Math.max(1, Math.ceil(count / Number(pageSize)))
  return (
    <div className="mt-4 flex items-center justify-between text-sm text-muted-foreground">
      <span>
        {count} {noun}
        {count === 1 ? '' : 's'} · page {page} of {pageCount}
      </span>
      <div className="flex gap-2">
        <Button variant="outline" size="sm" type="button" disabled={page <= 1} onClick={() => onChange(page - 1)}>
          Previous
        </Button>
        <Button variant="outline" size="sm" type="button" disabled={page >= pageCount} onClick={() => onChange(page + 1)}>
          Next
        </Button>
      </div>
    </div>
  )
}
