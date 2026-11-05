import { setupServer } from 'msw/node'

import { handlers } from './handlers'

/** Servidor MSW para Vitest: `server.listen()` en beforeAll y `resetDb()` en beforeEach. */
export const server = setupServer(...handlers)
