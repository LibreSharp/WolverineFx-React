import { useState } from 'react'
import { useCompleteTodo, useCreateTodo, useTodos } from './todoQueries'
import { useTodoChanges } from './useTodoChanges'

export function TodoList() {
  const [title, setTitle] = useState('')
  const todos = useTodos()
  const createTodo = useCreateTodo()
  const completeTodo = useCompleteTodo()

  // Live updates: refetch when this or another tab creates or completes a Todo.
  useTodoChanges()

  function addTodo(event: React.FormEvent) {
    event.preventDefault()
    if (!title.trim()) return
    createTodo.mutate(title, { onSuccess: () => setTitle('') })
  }

  return (
    <div className="mx-auto max-w-xl px-6 py-10">
      <h1 className="mb-4 text-2xl font-semibold">Todos</h1>

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
                <button
                  type="button"
                  onClick={() => completeTodo.mutate(todo.id!)}
                  disabled={completeTodo.isPending}
                  className="text-sm text-indigo-600 hover:underline disabled:opacity-50"
                >
                  Complete
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
