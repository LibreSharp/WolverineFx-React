import { useState, type ReactNode } from 'react'
import { useCompleteTodo, useCreateTodo, useRemindTodo, useTodos } from './todoQueries'
import { useTodoRealtime } from './useTodoRealtime'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

// "Remind me" choices, in minutes.
const reminderChoices = [
  { minutes: 1, label: 'in 1 minute' },
  { minutes: 60, label: 'in 1 hour' },
  { minutes: 24 * 60, label: 'tomorrow' },
]

// With plans (--Auth saas), PlanTodoList passes what the organisation's plan allows: whether it
// includes reminders (if not, reminderUpsell shows instead of the "Remind me" menu), and a notice
// about its limits. Without plans, everything is on.
export interface TodoListProps {
  remindersEnabled?: boolean
  reminderUpsell?: ReactNode
  notice?: ReactNode
  onReminderSet?: () => void
}

export function TodoList({ remindersEnabled = true, reminderUpsell, notice, onReminderSet }: TodoListProps = {}) {
  const [title, setTitle] = useState('')
  const todos = useTodos()
  const createTodo = useCreateTodo()
  const completeTodo = useCompleteTodo()
  const remindTodo = useRemindTodo()

  // Live updates: refetch when this or another tab creates or completes a Todo, and collect
  // reminders as they come due.
  const { reminders, dismiss } = useTodoRealtime()

  function addTodo(event: React.FormEvent) {
    event.preventDefault()
    if (!title.trim()) return
    createTodo.mutate(title, { onSuccess: () => setTitle('') })
  }

  return (
    <div className="mx-auto max-w-xl px-6 py-10">
      <h1 className="mb-4 text-2xl font-semibold">Todos</h1>

      {notice}

      <form onSubmit={addTodo} className="mb-4 flex gap-2">
        <Input
          className="flex-1"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="What needs doing?"
        />
        <Button type="submit" disabled={createTodo.isPending}>
          Add
        </Button>
      </form>

      {reminders.map((reminder) => (
        <div
          key={reminder.todoId}
          role="alert"
          className="mb-4 flex items-center justify-between rounded-lg border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-900 dark:text-amber-200"
        >
          <span>Reminder: {reminder.title}</span>
          <Button variant="link" type="button" onClick={() => dismiss(reminder.todoId)} className="h-auto px-0">
            Dismiss
          </Button>
        </div>
      ))}

      {remindTodo.isSuccess && (
        <p role="status" className="mb-4 text-sm text-muted-foreground">
          Reminder set for {new Date(remindTodo.data.remindAt).toLocaleString()}.
        </p>
      )}
      {remindTodo.error && (
        <p role="alert" className="mb-4 text-sm text-destructive">
          {remindTodo.error.message}
        </p>
      )}

      {createTodo.error && (
        <p role="alert" className="mb-4 text-sm text-destructive">
          {createTodo.error.message}
        </p>
      )}

      {todos.isPending ? (
        <p className="text-muted-foreground">Loading…</p>
      ) : todos.isError ? (
        <p role="alert" className="text-destructive">
          {todos.error.message}
        </p>
      ) : (
        <ul className="space-y-2">
          {todos.data.map((todo) => (
            <li
              key={todo.id}
              className="flex items-center justify-between rounded-lg border bg-card px-3 py-2"
            >
              <span className={todo.isComplete ? 'text-muted-foreground line-through' : ''}>
                {todo.title}
              </span>
              {!todo.isComplete && (
                <div className="flex items-center gap-3">
                  {remindersEnabled ? (
                    <NativeSelect
                      aria-label={`Remind me about ${todo.title}`}
                      value=""
                      onChange={(event) =>
                        remindTodo.mutate({ id: todo.id!, minutes: Number(event.target.value) }, { onSettled: onReminderSet })
                      }
                    >
                      <NativeSelectOption value="" disabled>
                        Remind me…
                      </NativeSelectOption>
                      {reminderChoices.map((choice) => (
                        <NativeSelectOption key={choice.minutes} value={choice.minutes}>
                          {choice.label}
                        </NativeSelectOption>
                      ))}
                    </NativeSelect>
                  ) : (
                    reminderUpsell
                  )}
                  <Button variant="link"
                    type="button"
                    onClick={() => completeTodo.mutate(todo.id!)}
                    disabled={completeTodo.isPending}
                    className="h-auto px-0"
                  >
                    Complete
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
