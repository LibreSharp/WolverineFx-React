import { useEffect, useState } from 'react'
import { api } from '../../lib/api/client'
import type { components } from '../../lib/api/schema'

type Todo = components['schemas']['Todo']

export function TodoList() {
  const [todos, setTodos] = useState<Todo[]>([])
  const [title, setTitle] = useState('')
  const [loading, setLoading] = useState(true)

  async function refresh() {
    const { data } = await api.GET('/api/todos')
    setTodos(data ?? [])
    setLoading(false)
  }

  useEffect(() => {
    refresh()
  }, [])

  async function addTodo(event: React.FormEvent) {
    event.preventDefault()
    if (!title.trim()) return
    await api.POST('/api/todos', { body: { title } })
    setTitle('')
    await refresh()
  }

  async function completeTodo(id: string) {
    await api.POST('/api/todos/{id}/complete', { params: { path: { id } } })
    await refresh()
  }

  return (
    <div className="mx-auto max-w-md p-8">
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
          className="rounded bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700"
        >
          Add
        </button>
      </form>

      {loading ? (
        <p className="text-gray-500">Loading…</p>
      ) : (
        <ul className="space-y-2">
          {todos.map((todo) => (
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
                  onClick={() => completeTodo(todo.id!)}
                  className="text-sm text-indigo-600 hover:underline"
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
