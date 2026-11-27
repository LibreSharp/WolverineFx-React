import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ErrorText, Notice } from '../../components/Messages'
import { Section } from '../../components/Section'
import { useDiscardMessages, useFailedMessages, useReplayMessages } from './adminQueries'
import { Pager } from './Pager'

// The message type's own name, without its namespace: "ApiHost.Billing.Payments.PaymentEventReceived" -> "PaymentEventReceived".
const shortName = (type: string) => type.split(/[.+]/).pop() ?? type

// /admin/failed-messages: messages that failed every attempt (Wolverine's dead-letter queue), such
// as an email that couldn't be sent. Fix the cause, then replay them, or discard them.
export function FailedMessagesPage() {
  const [page, setPage] = useState(1)
  const messages = useFailedMessages(page)
  const replay = useReplayMessages()
  const discard = useDiscardMessages()

  return (
    <Section title="Failed messages" description="Background work that failed every attempt. Replay a message once its cause is fixed.">
      <ErrorText error={messages.error ?? replay.error ?? discard.error} />
      {replay.isSuccess && <Notice className="mb-3">Sent again. It may take a few seconds to run.</Notice>}
      {messages.data && (
        <>
          {messages.data.messages.length === 0 ? (
            <p className="text-sm text-muted-foreground">None: everything has run.</p>
          ) : (
            <ul className="divide-y divide-border">
              {messages.data.messages.map((message) => (
                <li key={message.id} className="flex flex-col gap-2 py-3 sm:flex-row sm:items-start sm:gap-4">
                  <div className="min-w-0 flex-1 text-sm">
                    <div className="flex items-center gap-2 font-medium">
                      {shortName(message.messageType)}
                      {Number(message.replayCount) > 0 && <Badge variant="secondary">replayed {message.replayCount}×</Badge>}
                    </div>
                    <div className="text-muted-foreground">
                      {message.exceptionType && shortName(message.exceptionType)}: {message.exceptionMessage}
                    </div>
                    <div className="text-xs text-muted-foreground">
                      Failed {new Date(message.failedAt ?? message.sentAt).toLocaleString()}
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Button size="sm" disabled={replay.isPending || !message.replayable} onClick={() => replay.mutate([message.id])}>
                      Replay
                    </Button>
                    <Button size="sm" variant="outline" disabled={discard.isPending} onClick={() => discard.mutate([message.id])}>
                      Discard
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}
          <Pager total={messages.data.total} noun="message" page={page} pageSize={messages.data.pageSize} onChange={setPage} />
        </>
      )}
    </Section>
  )
}
