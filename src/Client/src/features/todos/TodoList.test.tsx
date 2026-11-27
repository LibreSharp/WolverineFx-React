import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { server } from '../../test/server'
import { renderPage } from '../../test/render'
import { TodoList } from './TodoList'

// No SignalR connection in unit tests; reminders are shown from what the hook returns.
const realtime = vi.hoisted(() => ({ reminders: [] as { todoId: string; title: string }[], dismiss: vi.fn() }))
vi.mock('./useTodoRealtime', () => ({ useTodoRealtime: () => realtime }))

type Todo = { id: string; title: string; isComplete: boolean }

describe('TodoList', () => {
  let todos: Todo[]

  beforeEach(() => {
    todos = [
      { id: '1', title: 'Write tests', isComplete: false },
      { id: '2', title: 'Ship it', isComplete: true },
    ]
    realtime.reminders = []
    server.use(http.get('*/api/todos', () => HttpResponse.json(todos)))
  })

  it('lists the todos, crossing out completed ones', async () => {
    renderPage(<TodoList />, '/todos')

    expect(await screen.findByText('Write tests')).toBeInTheDocument()
    expect(screen.getByText('Ship it')).toHaveClass('line-through')
    // Only open todos can be completed.
    expect(screen.getAllByRole('button', { name: 'Complete' })).toHaveLength(1)
  })

  it('adds a todo and shows the refreshed list', async () => {
    server.use(
      http.post('*/api/todos', async ({ request }) => {
        const { title } = (await request.json()) as { title: string }
        todos = [...todos, { id: '3', title, isComplete: false }]
        return HttpResponse.json({ id: '3' }, { status: 201 })
      }),
    )
    renderPage(<TodoList />, '/todos')
    await screen.findByText('Write tests')

    await userEvent.type(screen.getByPlaceholderText('What needs doing?'), 'Buy milk')
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByText('Buy milk')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('What needs doing?')).toHaveValue('')
  })

  it('shows the API validation message when adding fails', async () => {
    server.use(
      http.post('*/api/todos', () =>
        HttpResponse.json({ title: 'Validation failed', errors: { Title: ['Title is too long.'] } }, { status: 400 }),
      ),
    )
    renderPage(<TodoList />, '/todos')
    await screen.findByText('Write tests')

    await userEvent.type(screen.getByPlaceholderText('What needs doing?'), 'x')
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Title is too long.')
  })

  it('completes a todo', async () => {
    server.use(
      http.post('*/api/todos/:id/complete', ({ params }) => {
        todos = todos.map((t) => (t.id === params.id ? { ...t, isComplete: true } : t))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderPage(<TodoList />, '/todos')
    await screen.findByText('Write tests')

    await userEvent.click(screen.getByRole('button', { name: 'Complete' }))

    await waitFor(() => expect(screen.getByText('Write tests')).toHaveClass('line-through'))
  })

  it('sets a reminder and confirms when it is due', async () => {
    let minutes = 0
    server.use(
      http.post('*/api/todos/:id/remind', async ({ request }) => {
        minutes = ((await request.json()) as { minutes: number }).minutes
        return HttpResponse.json({ remindAt: '2030-01-01T10:00:00Z' })
      }),
    )
    renderPage(<TodoList />, '/todos')
    await screen.findByText('Write tests')

    await userEvent.selectOptions(screen.getByLabelText('Remind me about Write tests'), 'in 1 hour')

    expect(await screen.findByRole('status')).toHaveTextContent('Reminder set for')
    expect(minutes).toBe(60)
  })

  it('shows reminders as they arrive, and dismisses them', async () => {
    realtime.reminders = [{ todoId: '1', title: 'Write tests' }]
    renderPage(<TodoList />, '/todos')

    expect(await screen.findByRole('alert')).toHaveTextContent('Reminder: Write tests')
    await userEvent.click(screen.getByRole('button', { name: 'Dismiss' }))
    expect(realtime.dismiss).toHaveBeenCalledWith('1')
  })

  it('says so when the list cannot be loaded', async () => {
    server.use(http.get('*/api/todos', () => new HttpResponse(null, { status: 500 })))
    renderPage(<TodoList />, '/todos')

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load your todos.')
  })
})
