import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import {
  AxiosError,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from '@prestamos/api-client'

import { setupApi } from '@/app/api'
import { PERMISSIONS } from '@/app/permissions'
import { useSessionStore } from '@/app/stores/session'
import { useApprovals } from '@/features/approvals/composables/useApprovals'
import { approvalsService } from '@/features/approvals/services/approvals.service'

function ok(config: InternalAxiosRequestConfig, data: unknown): Promise<AxiosResponse> {
  return Promise.resolve({ data, status: 200, statusText: 'OK', headers: {}, config })
}

function fail(config: InternalAxiosRequestConfig, status: number, data: unknown): Promise<never> {
  const response = { data, status, statusText: 'Error', headers: {}, config } as AxiosResponse
  return Promise.reject(new AxiosError(`HTTP ${status}`, String(status), config, null, response))
}

function seedAuthenticatedSession(userId: string, permissions: string[]) {
  const session = useSessionStore()
  session.user = {
    id: userId,
    userId,
    fullName: 'Supervisor',
    email: 'sup@sistema.local',
    role: 'ROLE_ADMIN',
    mfaEnabled: true,
  }
  session.permissions = permissions
  return session
}

describe('Approvals Four-Eyes (API_SECURITY.md)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('approvalsService envía Idempotency-Key en approve, reject y cancel', async () => {
    const calls: InternalAxiosRequestConfig[] = []
    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'tok', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        calls.push(config)
        return ok(config, { id: 'req-1', status: 'APPROVED', result: null })
      },
    })

    await approvalsService.approveApproval('req-1', { mfaCode: '123456', comment: 'OK' })
    await approvalsService.rejectApproval('req-1', { comment: 'Falta sustento' })
    await approvalsService.cancelApproval('req-1')

    expect(calls.map((c) => c.url)).toEqual([
      '/approvals/req-1/approve',
      '/approvals/req-1/reject',
      '/approvals/req-1/cancel',
    ])
    for (const c of calls) {
      expect(c.headers.get('Idempotency-Key')).toBeTruthy()
    }
  })

  it('aplica reglas de cuatro ojos: bloquea auto-aprobación, auto-beneficio y ausencia de permiso', () => {
    seedAuthenticatedSession('user-me', [
      PERMISSIONS.approvalsRead,
      PERMISSIONS.approvalsUsersRole,
    ])
    const approvals = useApprovals()

    const selfRequested = {
      id: 'a1',
      operationType: 'USER_ROLE_CHANGE',
      targetEntity: 'User',
      targetId: 'user-other',
      status: 'PENDING' as const,
      reason: 'Cambio de rol de usuario',
      evidenceRef: null,
      requestedBy: { id: 'user-me', fullName: 'Yo', email: 'yo@sistema.local' },
      requestedAt: '2026-11-14T00:00:00Z',
      expiresAt: '2026-11-15T00:00:00Z',
      decidedBy: null,
      decidedAt: null,
    }
    expect(approvals.canDecide(selfRequested)).toBe(false)
    expect(approvals.canCancel(selfRequested)).toBe(true)

    const selfBeneficiary = {
      ...selfRequested,
      id: 'a2',
      targetId: 'user-me',
      requestedBy: { id: 'user-other', fullName: 'Otro', email: 'otro@sistema.local' },
    }
    expect(approvals.canDecide(selfBeneficiary)).toBe(false)

    const missingPerm = {
      ...selfRequested,
      id: 'a3',
      operationType: 'PAYMENT_REVERSAL',
      targetId: 'pay-1',
      requestedBy: { id: 'user-other', fullName: 'Otro', email: 'otro@sistema.local' },
    }
    expect(approvals.canDecide(missingPerm)).toBe(false)

    const allowed = {
      ...selfRequested,
      id: 'a4',
      targetId: 'user-other',
      requestedBy: { id: 'user-someone', fullName: 'Alguien', email: 'alguien@sistema.local' },
    }
    expect(approvals.canDecide(allowed)).toBe(true)
  })

  it('mantiene el modal abierto con error inline ante 403 Approvals.InvalidMfaCode y exige comentario en rechazo', async () => {
    seedAuthenticatedSession('approver-1', [
      PERMISSIONS.approvalsRead,
      PERMISSIONS.approvalsUsersRole,
    ])

    setupApi({
      baseURL: '/api',
      session: { getAccessToken: () => 'tok', refresh: async () => false, onSessionExpired: vi.fn<() => void>() },
      adapter: (config) => {
        if (config.url === '/approvals/req-1/approve') {
          return fail(config, 403, {
            title: 'Código MFA inválido',
            detail: 'El código TOTP ingresado no es válido.',
            code: 'Approvals.InvalidMfaCode',
          })
        }
        if (config.url === '/approvals/req-1/reject') {
          return ok(config, { id: 'req-1', status: 'REJECTED', result: null })
        }
        return ok(config, { items: [], total: 0, page: 1, pageSize: 20 })
      },
    })

    const approvals = useApprovals()
    const item = {
      id: 'req-1',
      operationType: 'USER_ROLE_CHANGE',
      targetEntity: 'User',
      targetId: 'target-99',
      status: 'PENDING' as const,
      reason: 'Cambio de rol solicitado',
      evidenceRef: null,
      requestedBy: { id: 'req-user', fullName: 'Solicitante', email: 'sol@sistema.local' },
      requestedAt: '2026-11-14T00:00:00Z',
      expiresAt: '2026-11-15T00:00:00Z',
      decidedBy: null,
      decidedAt: null,
    }

    approvals.openApproveModal(item)
    approvals.approveMfaCode.value = '123' // menos de 6 dígitos
    await approvals.submitApprove()
    expect(approvals.approveError.value).toContain('6 dígitos')

    approvals.approveMfaCode.value = '999999'
    await approvals.submitApprove()
    expect(approvals.approveTarget.value).not.toBeNull()
    expect(approvals.approveError.value).toContain('TOTP')

    approvals.openRejectModal(item)
    approvals.rejectComment.value = '   '
    await approvals.submitReject()
    expect(approvals.rejectError.value).toContain('motivo')

    approvals.rejectComment.value = 'No autorizado por gerencia'
    await approvals.submitReject()
    expect(approvals.rejectTarget.value).toBeNull()
  })
})
