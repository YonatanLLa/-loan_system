import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'

import {
  AxiosError,
  type AxiosAdapter,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from '@prestamos/api-client'

import { setupApi } from '@/app/api'
import { useAdminSettings } from '@/features/admin/composables'
import AdminPage from '@/features/admin/pages/AdminPage.vue'
import { adminService } from '@/features/admin/services'

function jsonResponse(
  config: InternalAxiosRequestConfig,
  data: unknown,
  status = 200,
): Promise<AxiosResponse> {
  return Promise.resolve({ data, status, statusText: 'OK', headers: {}, config })
}

function errorResponse(
  config: InternalAxiosRequestConfig,
  status: number,
  data: unknown,
): Promise<never> {
  const response = { data, status, statusText: 'Error', headers: {}, config } as AxiosResponse
  return Promise.reject(new AxiosError(`HTTP ${status}`, String(status), config, null, response))
}

describe('Admin Security (API_SECURITY.md)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('adminService llama únicamente a los endpoints documentados en API_SECURITY.md con Idempotency-Key en mutaciones', async () => {
    const calls: InternalAxiosRequestConfig[] = []
    const userId = '550e8400-e29b-41d4-a716-446655440000'
    const adapter: AxiosAdapter = (config) => {
      calls.push(config)
      if (config.url === '/security/ping') {
        return jsonResponse(config, {
          module: 'Security',
          status: 'Ready',
        })
      }
      if (config.url === `/users/${userId}/role`) {
        return jsonResponse(
          config,
          {
            id: 'app-123',
            operationType: 'USER_ROLE_CHANGE',
            targetEntity: 'User',
            targetId: userId,
            status: 'PENDING',
            reason: 'Cambio de rol por rotación interna',
            evidenceRef: null,
            requestedBy: { id: 'admin-1', fullName: 'Admin', email: 'admin@sistema.local' },
            requestedAt: '2026-11-14T00:00:00Z',
            expiresAt: '2026-11-15T00:00:00Z',
            decidedBy: null,
            decidedAt: null,
            payload: { newRole: 'ROLE_AUDITOR' },
            targetSnapshot: { role: 'ROLE_ANALYST' },
            currentTargetState: { role: 'ROLE_ANALYST' },
            decisionComment: null,
          },
          202,
        )
      }
      return jsonResponse(config, null, 204)
    }

    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'tok', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter,
    })

    const ping = await adminService.pingSecurity()
    expect(ping.module).toBe('Security')

    await adminService.suspendUser(userId)
    await adminService.activateUser(userId)
    await adminService.resetUserMfa(userId)
    const approval = await adminService.requestUserRoleChange(userId, {
      newRole: 'ROLE_AUDITOR',
      reason: 'Cambio de rol por rotación interna',
    })

    expect(approval.status).toBe('PENDING')
    expect(calls.map((c) => `${c.method?.toUpperCase()} ${c.url}`)).toEqual([
      'GET /security/ping',
      `POST /users/${userId}/suspend`,
      `POST /users/${userId}/activate`,
      `POST /users/${userId}/mfa/reset`,
      `PUT /users/${userId}/role`,
    ])

    for (const mutation of calls.slice(1)) {
      expect(mutation.headers.get('Idempotency-Key')).toBeTruthy()
    }
  })

  it('useAdminSettings valida UUID, confirmación de acciones, longitud mínima de justificación y matriz de 6 roles', async () => {
    const userId = '550e8400-e29b-41d4-a716-446655440000'
    const adapter: AxiosAdapter = (config) => {
      if (config.url === '/security/ping') {
        return jsonResponse(config, {
          module: 'Security',
          status: 'Ready',
        })
      }
      if (config.url === `/users/${userId}/role`) {
        return jsonResponse(
          config,
          {
            id: 'app-role-1',
            operationType: 'USER_ROLE_CHANGE',
            targetEntity: 'User',
            targetId: userId,
            status: 'PENDING',
            reason: 'Promoción a auditoría interna',
            evidenceRef: null,
            requestedBy: { id: 'admin-1', fullName: 'Admin', email: 'admin@sistema.local' },
            requestedAt: '2026-11-14T00:00:00Z',
            expiresAt: '2026-11-15T00:00:00Z',
            decidedBy: null,
            decidedAt: null,
            payload: { newRole: 'ROLE_AUDITOR' },
            targetSnapshot: { role: 'ROLE_ANALYST' },
            currentTargetState: null,
            decisionComment: null,
          },
          202,
        )
      }
      return errorResponse(config, 404, { title: 'No encontrado' })
    }

    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'tok', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter,
    })

    const settings = useAdminSettings({ autoLoad: false })
    expect(settings.SECURITY_ROLES_MATRIX).toHaveLength(6)
    const adminMatrix = settings.SECURITY_ROLES_MATRIX.find((r) => r.role === 'ROLE_ADMIN')
    expect(adminMatrix?.permissions).toHaveLength(20)

    settings.requestActionConfirmation('suspend')
    expect(settings.targetUserIdError.value).toBeTruthy()

    settings.targetUserId.value = 'no-es-uuid'
    await settings.submitRoleChange()
    expect(settings.targetUserIdError.value).toContain('UUID')
    expect(settings.roleReasonError.value).toContain('10 caracteres')

    settings.targetUserId.value = userId
    settings.roleForm.newRole = 'ROLE_AUDITOR'
    settings.roleForm.reason = 'Promoción a auditoría interna'
    await settings.submitRoleChange()

    expect(settings.createdApproval.value?.id).toBe('app-role-1')
    expect(settings.roleForm.reason).toBe('')
  })

  it('AdminPage renderiza el estado de Security y la matriz de roles sin llamar a /admin/*', async () => {
    const urls: string[] = []
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'tok', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        urls.push(config.url ?? '')
        return jsonResponse(config, {
          module: 'Security',
          status: 'Ready',
        })
      },
    })

    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/administracion', name: 'admin', component: AdminPage },
        { path: '/auditoria', name: 'audit', component: { template: '<div />' } },
        { path: '/aprobaciones', name: 'approvals', component: { template: '<div />' } },
      ],
    })
    await router.push('/administracion')
    await router.isReady()

    const wrapper = mount(AdminPage, {
      global: { plugins: [router] },
    })

    expect(wrapper.text()).toContain('Administración de Seguridad')
    expect(wrapper.text()).toContain('ROLE_ADMIN')
    expect(wrapper.text()).toContain('approvals.users.role')
    expect(urls.every((u) => !u.startsWith('/admin'))).toBe(true)
  })
})
