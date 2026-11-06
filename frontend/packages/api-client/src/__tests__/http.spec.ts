import { describe, expect, it, vi } from 'vitest'
import { AxiosError, type AxiosAdapter, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'

import { ApiError } from '../errors'
import { createApiClient, type SessionPort } from '../http'

function ok(config: InternalAxiosRequestConfig, data: unknown): Promise<AxiosResponse> {
  return Promise.resolve({ data, status: 200, statusText: 'OK', headers: {}, config })
}

function fail(config: InternalAxiosRequestConfig, status: number, data: unknown = {}): Promise<never> {
  const response = { data, status, statusText: '', headers: {}, config } as AxiosResponse
  return Promise.reject(new AxiosError(`HTTP ${status}`, String(status), config, null, response))
}

function fakeSession(overrides: Partial<SessionPort> = {}): SessionPort {
  return {
    getAccessToken: () => 'token-actual',
    refresh: vi.fn<() => Promise<boolean>>(async () => false),
    onSessionExpired: vi.fn<() => void>(),
    ...overrides,
  }
}

describe('createApiClient', () => {
  it('envía el token y un id de correlación en cada petición', async () => {
    const seen: InternalAxiosRequestConfig[] = []
    const adapter: AxiosAdapter = (config) => {
      seen.push(config)
      return ok(config, {})
    }
    const api = createApiClient({ baseURL: 'http://api.test', session: fakeSession(), adapter })

    await api.get('/clientes')

    expect(seen[0]?.headers.get('Authorization')).toBe('Bearer token-actual')
    expect(seen[0]?.headers.get('X-Correlation-Id')).toMatch(/^[0-9a-f-]{36}$/)
  })

  it('comparte un solo refresco entre peticiones concurrentes que reciben 401', async () => {
    let token = 'vencido'
    const refresh = vi.fn<() => Promise<boolean>>(
      () =>
        new Promise<boolean>((resolve) =>
          setTimeout(() => {
            token = 'nuevo'
            resolve(true)
          }, 10),
        ),
    )
    const adapter: AxiosAdapter = (config) =>
      config.headers.get('Authorization') === 'Bearer nuevo'
        ? ok(config, { url: config.url })
        : fail(config, 401)
    const api = createApiClient({
      baseURL: 'http://api.test',
      session: fakeSession({ getAccessToken: () => token, refresh }),
      adapter,
    })

    const [a, b] = await Promise.all([api.get('/a'), api.get('/b')])

    expect(refresh).toHaveBeenCalledTimes(1)
    expect(a.data).toEqual({ url: '/a' })
    expect(b.data).toEqual({ url: '/b' })
  })

  it('cierra la sesión si el refresco falla', async () => {
    const session = fakeSession({ refresh: vi.fn<() => Promise<boolean>>(async () => false) })
    const adapter: AxiosAdapter = (config) => fail(config, 401)
    const api = createApiClient({ baseURL: 'http://api.test', session, adapter })

    await expect(api.get('/me')).rejects.toBeInstanceOf(ApiError)
    expect(session.onSessionExpired).toHaveBeenCalledTimes(1)
  })

  it('no intenta refrescar cuando la petición lo pide (login)', async () => {
    const session = fakeSession()
    const adapter: AxiosAdapter = (config) => fail(config, 401, { title: 'Credenciales inválidas' })
    const api = createApiClient({ baseURL: 'http://api.test', session, adapter })

    await expect(api.post('/auth/login', {}, { skipAuthRefresh: true })).rejects.toMatchObject({
      status: 401,
      title: 'Credenciales inválidas',
    })
    expect(session.refresh).not.toHaveBeenCalled()
  })
})
