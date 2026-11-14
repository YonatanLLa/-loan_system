import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'

import { type AxiosResponse, type InternalAxiosRequestConfig } from '@prestamos/api-client'

import { setupApi } from '@/app/api'
import { useAuditLog } from '@/features/audit/composables/useAuditLog'
import AuditLogPage from '@/features/audit/pages/AuditLogPage.vue'
import { auditService } from '@/features/audit/services/audit.service'

function ok(config: InternalAxiosRequestConfig, data: unknown): Promise<AxiosResponse> {
  return Promise.resolve({ data, status: 200, statusText: 'OK', headers: {}, config })
}

describe('Audit feature (API_SECURITY.md GET /api/audit-logs)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('auditService.getLogs consulta solo GET /audit-logs y nunca llama a /audit/entities o /audit/actions', async () => {
    const requests: InternalAxiosRequestConfig[] = []
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'tok', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        requests.push(config)
        return ok(config, { items: [], total: 0, page: 1, pageSize: 20 })
      },
    })

    await auditService.getLogs({
      entity: 'users',
      entityId: 'u-1',
      action: 'UPDATE',
      userId: '550e8400-e29b-41d4-a716-446655440000',
      approvalRequestId: '660e8400-e29b-41d4-a716-446655440001',
      page: 1,
      pageSize: 20,
    })

    expect(requests).toHaveLength(1)
    expect(requests[0]?.url).toBe('/audit-logs')
    expect(requests[0]?.params).toEqual({
      page: 1,
      pageSize: 20,
      entity: 'users',
      entityId: 'u-1',
      action: 'UPDATE',
      userId: '550e8400-e29b-41d4-a716-446655440000',
      approvalRequestId: '660e8400-e29b-41d4-a716-446655440001',
    })
  })

  it('useAuditLog distingue userId (UUID) vs user (texto) y valida approvalRequestId antes de enviar', async () => {
    const paramsLog: unknown[] = []
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'tok', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        paramsLog.push(config.params)
        return ok(config, { items: [], total: 0, page: 1, pageSize: 20 })
      },
    })

    const audit = useAuditLog()

    // Caso 1: approvalRequestId inválido -> no llama API
    audit.filters.value.approvalRequestId = 'invalido'
    await audit.loadAuditLogs()
    expect(audit.approvalRequestIdError.value).toContain('UUID')
    expect(paramsLog).toHaveLength(0)

    // Caso 2: UUID válido en campo usuario -> envía userId
    audit.filters.value.approvalRequestId = ''
    audit.filters.value.user = '550e8400-e29b-41d4-a716-446655440000'
    await audit.loadAuditLogs()
    expect(paramsLog[0]).toMatchObject({
      userId: '550e8400-e29b-41d4-a716-446655440000',
    })
    expect((paramsLog[0] as Record<string, unknown>).user).toBeUndefined()

    // Caso 3: texto de correo/nombre -> envía user
    audit.filters.value.user = 'admin@sistema.local'
    await audit.loadAuditLogs()
    expect(paramsLog[1]).toMatchObject({ user: 'admin@sistema.local' })
    expect((paramsLog[1] as Record<string, unknown>).userId).toBeUndefined()
  })

  it('AuditLogPage renderiza campos opcionales nulos sin errores', async () => {
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'tok', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) =>
        ok(config, {
          items: [
            {
              id: 42,
              entityName: 'users',
              entityId: 'u-99',
              action: 'UPDATE',
              stateBefore: { status: 'ACTIVE' },
              stateAfter: { status: 'SUSPENDED' },
              userId: null,
              userName: null,
              userEmail: null,
              userIp: null,
              userAgent: null,
              createdAt: '2026-11-14T00:00:00Z',
              approvalRequestId: null,
            },
          ],
          total: 1,
          page: 1,
          pageSize: 20,
        }),
    })

    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/auditoria', name: 'audit', component: AuditLogPage },
        { path: '/aprobaciones', name: 'approvals', component: { template: '<div />' } },
      ],
    })
    await router.push('/auditoria')
    await router.isReady()

    const wrapper = mount(AuditLogPage, { global: { plugins: [router] } })
    await flushPromises()

    expect(wrapper.text()).toContain('Sistema')
    expect(wrapper.text()).toContain('Sin IP')
    expect(wrapper.text()).toContain('#42')
  })
})
