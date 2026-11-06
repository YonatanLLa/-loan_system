import { http } from 'msw'

import { authHandlers } from './auth'
import { catalogHandlers } from './catalog'
import { clientHandlers } from './client'
import { collectionsReportHandlers } from './collections-reports'
import { customerHandlers } from './customers'
import { loanHandlers } from './loans'
import { originationHandlers } from './origination'
import { paymentHandlers } from './payments'
import { treasuryHandlers } from './treasury'
import { problem } from '../http-utils'

export const handlers = [
  ...authHandlers,
  ...customerHandlers,
  ...catalogHandlers,
  ...originationHandlers,
  ...loanHandlers,
  ...paymentHandlers,
  ...treasuryHandlers,
  ...collectionsReportHandlers,
  ...clientHandlers,
  http.all('*/api/*', ({ request }) =>
    problem(
      501,
      'Endpoint simulado no disponible',
      `No hay un mock configurado para ${request.method} ${new URL(request.url).pathname}.`,
    ),
  ),
]
