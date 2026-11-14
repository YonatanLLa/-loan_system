import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import {
  ApiError,
  AxiosError,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from '@prestamos/api-client'
import { setupApi } from '@/app/api'
import { useSessionStore } from '@/app/stores/session'

function ok(config: InternalAxiosRequestConfig, data: unknown): Promise<AxiosResponse> {
  return Promise.resolve({ data, status: 200, statusText: 'OK', headers: {}, config })
}

function fail(config: InternalAxiosRequestConfig, status: number, data: unknown): Promise<never> {
  const response = { data, status, statusText: 'Error', headers: {}, config } as AxiosResponse
  return Promise.reject(new AxiosError(`HTTP ${status}`, String(status), config, null, response))
}

describe('useSessionStore (Security auth, MFA & sessionStorage refresh rotation)', () => {
  beforeEach(() => {
    sessionStorage.clear()
    setActivePinia(createPinia())
  })

  it('persiste refreshToken en sessionStorage tras login AUTHENTICATED y carga perfil con mfaEnabled', async () => {
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/auth/login') {
          return ok(config, {
            status: 'AUTHENTICATED',
            accessToken: 'acc-1',
            refreshToken: 'ref-1',
            expiresInSeconds: 900,
            role: 'ROLE_ADMIN',
            mfaToken: null,
            mfaExpiresInSeconds: null,
          })
        }
        if (config.url === '/me') {
          return ok(config, {
            id: 'user-1',
            userId: 'user-1',
            fullName: 'Ana Admin',
            email: 'admin@sistema.local',
            role: 'ROLE_ADMIN',
            permissions: ['admin.manage', 'approvals.read'],
            mfaEnabled: true,
          })
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    const status = await session.login('admin@sistema.local', 'Password123!')

    expect(status).toBe('AUTHENTICATED')
    expect(session.accessToken).toBe('acc-1')
    expect(sessionStorage.getItem('prestamos.backoffice.refreshToken')).toBe('ref-1')
    expect(session.user?.mfaEnabled).toBe(true)
    expect(session.can('approvals.read')).toBe(true)
  })

  it('rechaza cuentas ROLE_CLIENT y revoca su token inmediatamente', async () => {
    const logoutCalls: unknown[] = []
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/auth/login') {
          return ok(config, {
            status: 'AUTHENTICATED',
            accessToken: 'client-acc',
            refreshToken: 'client-ref',
            expiresInSeconds: 900,
            role: 'ROLE_CLIENT',
            mfaToken: null,
            mfaExpiresInSeconds: null,
          })
        }
        if (config.url === '/auth/logout') {
          logoutCalls.push(JSON.parse(config.data as string))
          return ok(config, null)
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    await expect(session.login('cliente@correo.pe', 'Demo1234!')).rejects.toBeInstanceOf(ApiError)
    expect(logoutCalls).toEqual([{ refreshToken: 'client-ref' }])
    expect(session.isAuthenticated).toBe(false)
    expect(sessionStorage.getItem('prestamos.backoffice.refreshToken')).toBeNull()
  })

  it('maneja MFA_ENROLLMENT_REQUIRED sin re-rotar secreto al recargar y completa con recoveryCodes', async () => {
    let enrollCount = 0
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/auth/login') {
          return ok(config, {
            status: 'MFA_ENROLLMENT_REQUIRED',
            accessToken: null,
            refreshToken: null,
            expiresInSeconds: null,
            role: null,
            mfaToken: 'mfa-tok-enroll',
            mfaExpiresInSeconds: 300,
          })
        }
        if (config.url === '/auth/mfa/enroll') {
          enrollCount += 1
          return ok(config, {
            secret: 'JBSWY3DPEHPK3PXP',
            otpauthUri: 'otpauth://totp/Sistema:admin@sistema.local?secret=JBSWY3DPEHPK3PXP',
          })
        }
        if (config.url === '/auth/mfa/enroll/confirm') {
          return ok(config, {
            tokens: {
              accessToken: 'acc-enrolled',
              refreshToken: 'ref-enrolled',
              expiresInSeconds: 900,
              role: 'ROLE_ADMIN',
            },
            recoveryCodes: ['AAAA-BBBB-CCCC', 'DDDD-EEEE-FFFF'],
          })
        }
        if (config.url === '/me') {
          return ok(config, {
            id: 'u1',
            fullName: 'Admin',
            email: 'admin@sistema.local',
            role: 'ROLE_ADMIN',
            permissions: ['admin.manage'],
            mfaEnabled: true,
          })
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    const status = await session.login('admin@sistema.local', 'Demo1234!')
    expect(status).toBe('MFA_ENROLLMENT_REQUIRED')
    expect(sessionStorage.getItem('prestamos.backoffice.mfaChallenge')).toContain('mfa-tok-enroll')

    const firstEnroll = await session.startMfaEnroll()
    const secondEnroll = await session.startMfaEnroll()
    expect(enrollCount).toBe(1)
    expect(secondEnroll.secret).toBe(firstEnroll.secret)

    const codes = await session.confirmMfaEnroll('123456')
    expect(codes).toEqual(['AAAA-BBBB-CCCC', 'DDDD-EEEE-FFFF'])
    expect(session.accessToken).toBe('acc-enrolled')
    expect(sessionStorage.getItem('prestamos.backoffice.refreshToken')).toBe('ref-enrolled')
    expect(sessionStorage.getItem('prestamos.backoffice.mfaChallenge')).toBeNull()
  })

  it('ejecuta refresh single-flight compartido entre refreshAccessToken y ensure, rotando el refreshToken', async () => {
    sessionStorage.setItem('prestamos.backoffice.refreshToken', 'ref-old')
    let refreshCalls = 0
    const refreshBodies: unknown[] = []

    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: async (config) => {
        if (config.url === '/auth/refresh') {
          refreshCalls += 1
          refreshBodies.push(JSON.parse(config.data as string))
          await new Promise((r) => setTimeout(r, 15))
          return ok(config, {
            accessToken: 'acc-new',
            refreshToken: 'ref-new',
            expiresInSeconds: 900,
            role: 'ROLE_ADMIN',
          })
        }
        if (config.url === '/me') {
          return ok(config, {
            id: 'u1',
            fullName: 'Admin',
            email: 'admin@sistema.local',
            role: 'ROLE_ADMIN',
            permissions: ['dashboard.view'],
            mfaEnabled: true,
          })
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    const [okRefresh, okEnsure] = await Promise.all([
      session.refreshAccessToken(),
      session.ensure(),
    ])

    expect(okRefresh).toBe(true)
    expect(okEnsure).toBe(true)
    expect(refreshCalls).toBe(1)
    expect(refreshBodies).toEqual([{ refreshToken: 'ref-old' }])
    expect(sessionStorage.getItem('prestamos.backoffice.refreshToken')).toBe('ref-new')
  })

  it('detecta Security.RefreshTokenReused, limpia storage y marca sessionTerminationReason="reused"', async () => {
    sessionStorage.setItem('prestamos.backoffice.refreshToken', 'stolen-ref')

    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => null, refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) =>
        fail(config, 401, {
          title: 'Token comprometido',
          detail: 'Refresh token reused',
          code: 'Security.RefreshTokenReused',
        }),
    })

    const session = useSessionStore()
    const okRefresh = await session.refreshAccessToken()

    expect(okRefresh).toBe(false)
    expect(session.sessionTerminationReason).toBe('reused')
    expect(sessionStorage.getItem('prestamos.backoffice.refreshToken')).toBeNull()
  })

  it('logout envía { refreshToken } y limpia la sesión incluso si la API falla', async () => {
    sessionStorage.setItem('prestamos.backoffice.refreshToken', 'ref-active')
    const sentBodies: unknown[] = []

    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'acc', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/auth/logout') {
          sentBodies.push(JSON.parse(config.data as string))
          return fail(config, 500, { title: 'Error interno' })
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    await session.logout()

    expect(sentBodies).toEqual([{ refreshToken: 'ref-active' }])
    expect(session.accessToken).toBeNull()
    expect(sessionStorage.getItem('prestamos.backoffice.refreshToken')).toBeNull()
  })

  it('logoutAll envía {} (sin refreshToken) con skipAuthRefresh y limpia la sesión incluso si la API falla', async () => {
    sessionStorage.setItem('prestamos.backoffice.refreshToken', 'ref-active')
    const logoutRequests: Array<{ body: Record<string, unknown>; skipAuthRefresh?: boolean }> = []

    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'acc', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/auth/logout') {
          logoutRequests.push({
            body: JSON.parse(config.data as string) as Record<string, unknown>,
            skipAuthRefresh: config.skipAuthRefresh,
          })
          return fail(config, 500, { title: 'Error interno' })
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    session.accessToken = 'acc'
    session.user = {
      id: 'user-1',
      userId: 'user-1',
      fullName: 'Auditor Prueba',
      email: 'auditor@sistema.local',
      role: 'ROLE_AUDITOR',
      mfaEnabled: false,
    }

    await session.logoutAll()

    expect(logoutRequests).toEqual([{ body: {}, skipAuthRefresh: true }])
    expect(Object.prototype.hasOwnProperty.call(logoutRequests[0]?.body, 'refreshToken')).toBe(false)
    expect(session.accessToken).toBeNull()
    expect(session.user).toBeNull()
    expect(session.isAuthenticated).toBe(false)
    expect(sessionStorage.getItem('prestamos.backoffice.refreshToken')).toBeNull()
  })

  it('refreshProfile re-consulta GET /me y en modo demo conserva el usuario actual sin llamar a la API', async () => {
    let meCalls = 0
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'acc', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/me') {
          meCalls += 1
          return ok(config, {
            id: 'user-aud',
            userId: 'user-aud',
            fullName: meCalls === 1 ? 'Auditor Inicial' : 'Auditor Actualizado',
            email: 'auditor@sistema.local',
            role: 'ROLE_AUDITOR',
            permissions: meCalls === 1 ? ['dashboard.view'] : ['dashboard.view', 'audit.read'],
            mfaEnabled: meCalls > 1,
          })
        }
        return fail(config, 404, {})
      },
    })

    const session = useSessionStore()
    await session.loadProfile()
    expect(session.user?.fullName).toBe('Auditor Inicial')
    expect(session.permissions).toEqual(['dashboard.view'])

    await session.refreshProfile()
    expect(meCalls).toBe(2)
    expect(session.user?.fullName).toBe('Auditor Actualizado')
    expect(session.user?.mfaEnabled).toBe(true)
    expect(session.permissions).toEqual(['dashboard.view', 'audit.read'])

    // En modo demo no llama a GET /me y mantiene el usuario demo actual
    session.isDemo = true
    session.user = {
      id: 'demo',
      userId: 'demo',
      fullName: 'Usuario Demo',
      email: 'demo@prestamos.local',
      role: 'ROLE_ADMIN',
      mfaEnabled: true,
    }
    session.permissions = ['admin.manage']

    await session.refreshProfile()
    expect(meCalls).toBe(2)
    expect(session.user?.fullName).toBe('Usuario Demo')
    expect(session.permissions).toEqual(['admin.manage'])
  })
})
