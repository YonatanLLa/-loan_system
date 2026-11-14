import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import {
  AxiosError,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from '@prestamos/api-client'

import { setupApi } from '@/app/api'
import { useSessionStore } from '@/app/stores/session'
import { useClientAuth } from '@/features/onboarding/composables/useClientAuth'

function ok(config: InternalAxiosRequestConfig, data: unknown): Promise<AxiosResponse> {
  return Promise.resolve({ data, status: 200, statusText: 'OK', headers: {}, config })
}

function fail(config: InternalAxiosRequestConfig, status: number, data: unknown): Promise<never> {
  const response = { data, status, statusText: 'Error', headers: {}, config } as AxiosResponse
  return Promise.reject(new AxiosError(`HTTP ${status}`, String(status), config, null, response))
}

describe('Portal Session & Auth (ROLE_CLIENT + API_SECURITY.md)', () => {
  beforeEach(() => {
    sessionStorage.clear()
    setActivePinia(createPinia())
  })

  it('login() usa POST /api/auth/login, guarda refreshToken en sessionStorage y asigna kycStatus VERIFIED por defecto', async () => {
    const calledUrls: string[] = []
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        calledUrls.push(config.url ?? '')
        if (config.url === '/auth/login') {
          return ok(config, {
            status: 'AUTHENTICATED',
            accessToken: 'portal-acc',
            refreshToken: 'portal-ref',
            expiresInSeconds: 900,
            role: 'ROLE_CLIENT',
            mfaToken: null,
            mfaExpiresInSeconds: null,
          })
        }
        if (config.url === '/me') {
          return ok(config, {
            id: 'cli-1',
            userId: 'cli-1',
            fullName: 'María Quispe',
            email: 'cliente@correo.pe',
            role: 'ROLE_CLIENT',
            permissions: ['client.portal'],
          })
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    const auth = useClientAuth()
    auth.loginIdentifier.value = 'cliente@correo.pe'
    auth.loginPassword.value = 'Demo1234!'

    const success = await auth.login()

    expect(success).toBe(true)
    expect(calledUrls).toEqual(['/auth/login', '/me'])
    expect(session.accessToken).toBe('portal-acc')
    expect(sessionStorage.getItem('prestamos.portal.refreshToken')).toBe('portal-ref')
    expect(session.user?.kycStatus).toBe('VERIFIED')
    session.stopInactivityWatch()
  })

  it('rechaza cuentas internas (no ROLE_CLIENT) y revoca sus tokens en /auth/logout', async () => {
    const logoutBodies: unknown[] = []
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/auth/login') {
          return ok(config, {
            status: 'AUTHENTICATED',
            accessToken: 'staff-acc',
            refreshToken: 'staff-ref',
            expiresInSeconds: 900,
            role: 'ROLE_ADMIN',
            mfaToken: null,
            mfaExpiresInSeconds: null,
          })
        }
        if (config.url === '/auth/logout') {
          logoutBodies.push(JSON.parse(config.data as string))
          return ok(config, null)
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    const auth = useClientAuth()
    auth.loginIdentifier.value = 'admin@sistema.local'
    auth.loginPassword.value = 'Demo1234!'

    const success = await auth.login()

    expect(success).toBe(false)
    expect(auth.loginError.value).toContain('Backoffice')
    expect(logoutBodies).toEqual([{ refreshToken: 'staff-ref' }])
    expect(session.isAuthenticated).toBe(false)
    expect(sessionStorage.getItem('prestamos.portal.refreshToken')).toBeNull()
  })

  it('comparte un solo refresh single-flight en ensure() y detecta Security.RefreshTokenReused', async () => {
    sessionStorage.setItem('prestamos.portal.refreshToken', 'portal-old')
    let refreshCount = 0

    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: async (config) => {
        if (config.url === '/auth/refresh') {
          refreshCount += 1
          await new Promise((r) => setTimeout(r, 15))
          if (refreshCount === 1) {
            return ok(config, {
              accessToken: 'portal-acc-2',
              refreshToken: 'portal-ref-2',
              expiresInSeconds: 900,
              role: 'ROLE_CLIENT',
            })
          }
          return fail(config, 401, {
            title: 'Sesión comprometida',
            detail: 'Token reused',
            code: 'Security.RefreshTokenReused',
          })
        }
        if (config.url === '/me') {
          return ok(config, {
            id: 'cli-1',
            fullName: 'María Quispe',
            email: 'cliente@correo.pe',
            role: 'ROLE_CLIENT',
            permissions: ['client.portal'],
          })
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    const [r1, r2] = await Promise.all([session.refreshAccessToken(), session.ensure()])

    expect(r1).toBe(true)
    expect(r2).toBe(true)
    expect(refreshCount).toBe(1)
    expect(sessionStorage.getItem('prestamos.portal.refreshToken')).toBe('portal-ref-2')

    // Simular reuse detection en siguiente refresh
    const r3 = await session.refreshAccessToken()
    expect(r3).toBe(false)
    expect(session.sessionTerminationReason).toBe('reused')
    expect(sessionStorage.getItem('prestamos.portal.refreshToken')).toBeNull()
  })

  it('logout envía { refreshToken } al backend y limpia sessionStorage', async () => {
    const logoutPayloads: unknown[] = []
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'acc', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/auth/logout') {
          logoutPayloads.push(JSON.parse(config.data as string))
          return ok(config, null)
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    session.setSession('acc', 'ref-to-revoke')
    const auth = useClientAuth()
    await auth.logout()

    expect(logoutPayloads).toEqual([{ refreshToken: 'ref-to-revoke' }])
    expect(sessionStorage.getItem('prestamos.portal.refreshToken')).toBeNull()
    expect(session.accessToken).toBeNull()
  })
})
