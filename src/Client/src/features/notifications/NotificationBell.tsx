import { useNavigate } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { BellIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useRealtime } from '../../lib/realtime'
import { notificationKeys, useMarkRead, useNotifications } from './notificationQueries'

const timeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

// The header's notifications: a bell with the number unread, opening the latest. Opening one marks
// it read and goes to its page. The list refreshes as notifications arrive (over the tab's realtime
// connection).
export function NotificationBell() {
  const queryClient = useQueryClient()
  const notifications = useNotifications()
  const markRead = useMarkRead()
  const navigate = useNavigate()
  const refresh = () => void queryClient.invalidateQueries({ queryKey: notificationKeys.list })
  useRealtime((message) => {
    if (message.type === 'notification-added') refresh()
  }, refresh)

  const unread = Number(notifications.data?.unread ?? 0)
  const list = notifications.data?.notifications ?? []

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon-sm" aria-label={unread > 0 ? `Notifications, ${unread} unread` : 'Notifications'} className="relative">
          <BellIcon aria-hidden="true" />
          {unread > 0 && (
            <span aria-hidden="true" className="absolute -top-0.5 -right-0.5 min-w-4 rounded-full bg-primary px-1 text-center text-[10px] leading-4 text-primary-foreground">
              {unread > 9 ? '9+' : unread}
            </span>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-80">
        <div className="flex items-center justify-between">
          <DropdownMenuLabel>Notifications</DropdownMenuLabel>
          {unread > 0 && (
            <Button variant="link" size="sm" onClick={() => markRead.mutate(undefined)}>
              Mark all read
            </Button>
          )}
        </div>
        <DropdownMenuSeparator />
        {notifications.isError && <p className="px-2 py-3 text-sm text-destructive">{notifications.error.message}</p>}
        {notifications.isSuccess && list.length === 0 && <p className="px-2 py-3 text-sm text-muted-foreground">Nothing yet.</p>}
        {list.map((n) => (
          <DropdownMenuItem
            key={n.id}
            className="flex flex-col items-start gap-0.5"
            onSelect={() => {
              if (!n.read) markRead.mutate(n.id)
              if (n.link) void navigate(n.link)
            }}
          >
            <span className={n.read ? 'text-muted-foreground' : 'font-medium'}>{n.title}</span>
            {n.body && <span className="text-xs text-muted-foreground">{n.body}</span>}
            <span className="text-xs text-muted-foreground">{timeFormat.format(new Date(n.createdAt))}</span>
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
