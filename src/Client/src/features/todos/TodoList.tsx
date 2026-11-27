import { useState, type ReactNode } from 'react'
import { useCompleteTodo, useCreateTodo, useRemindTodo, useTodos } from './todoQueries'
import { useTodoRealtime } from './useTodoRealtime'

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
        <input
          className="flex-1 rounded border border-gray-300 px-3 py-2"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="What needs doing?"
        />
        <button
          type="submit"
          disabled={createTodo.isPending}
          className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          Add
        </button>
      </form>

      {reminders.map((reminder) => (
        <div
          key={reminder.todoId}
          role="alert"
          className="mb-4 flex items-center justify-between rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900"
        >
          <span>Reminder: {reminder.title}</span>
          <button type="button" onClick={() => dismiss(reminder.todoId)} className="hover:underline">
            Dismiss
          </button>
        </div>
      ))}

      {remindTodo.isSuccess && (
        <p role="status" className="mb-4 text-sm text-gray-600">
          Reminder set for {new Date(remindTodo.data.remindAt).toLocaleString()}.
        </p>
      )}
      {remindTodo.error && (
        <p role="alert" className="mb-4 text-sm text-red-600">
          {remindTodo.error.message}
        </p>
      )}

      {createTodo.error && (
        <p role="alert" className="mb-4 text-sm text-red-600">
          {createTodo.error.message}
        </p>
      )}

      {todos.isPending ? (
        <p className="text-gray-500">Loading…</p>
      ) : todos.isError ? (
        <p role="alert" className="text-red-600">
          {todos.error.message}
        </p>
      ) : (
        <ul className="space-y-2">
          {todos.data.map((todo) => (
            <li
              key={todo.id}
              className="flex items-center justify-between rounded border border-gray-200 px-3 py-2"
            >
              <span className={todo.isComplete ? 'text-gray-400 line-through' : ''}>
                {todo.title}
              </span>
              {!todo.isComplete && (
                <div className="flex items-center gap-3">
                  {remindersEnabled ? (
                    <select
                      aria-label={`Remind me about ${todo.title}`}
                      value=""
                      onChange={(event) =>
                        remindTodo.mutate({ id: todo.id!, minutes: Number(event.target.value) }, { onSettled: onReminderSet })
                      }
                      className="rounded border border-gray-200 bg-white px-1 py-0.5 text-sm text-gray-600"
                    >
                      <option value="" disabled>
                        Remind me…
                      </option>
                      {reminderChoices.map((choice) => (
                        <option key={choice.minutes} value={choice.minutes}>
                          {choice.label}
                        </option>
                      ))}
                    </select>
                  ) : (
                    reminderUpsell
                  )}
                  <button
                    type="button"
                    onClick={() => completeTodo.mutate(todo.id!)}
                    disabled={completeTodo.isPending}
                    className="text-sm text-indigo-600 hover:underline disabled:opacity-50"
                  >
                    Complete
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
