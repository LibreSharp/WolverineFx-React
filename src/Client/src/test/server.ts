import { setupServer } from 'msw/node'

// The fake API for unit tests. Each test adds the responses it needs with server.use(...), for
// example: server.use(http.get('*/api/todos', () => HttpResponse.json([]))).
export const server = setupServer()
