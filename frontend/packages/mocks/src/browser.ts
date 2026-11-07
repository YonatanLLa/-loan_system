import { setupWorker } from 'msw/browser'

import { handlers } from './handlers'
import { restoreSession } from './handlers/auth'
import { configureMocks } from './http-utils'
import { resetDb } from './seed'

export interface StartMocksOptions {
  /** Latencia simulada por respuesta, en ms (por defecto 200). */
  latencyMs?: number
}

/**
 * Activa la API simulada en el navegador. Las peticiones a `/api/*` las responde MSW con los
 * datos semilla; todo lo demás (Vite, assets) pasa sin tocarse.
 */
export async function startMocks(options: StartMocksOptions = {}): Promise<void> {
  configureMocks(options)
  resetDb()
  restoreSession()
  const worker = setupWorker(...handlers)
  await worker.start({
    onUnhandledFrame: 'bypass',
    quiet: true,
    serviceWorker: { url: '/mockServiceWorker.js' },
  })
  console.info('[mocks] API simulada activa. Clientes: cliente@correo.pe (con préstamo), nuevo@correo.pe (sin préstamo) · clave Demo1234!')
}
