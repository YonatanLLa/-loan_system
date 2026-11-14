import {
  type ApprovalRequestDto,
  type ChangeUserRoleRequest,
  type SecurityPingResponse,
  newIdempotencyKey,
  withIdempotency,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

/**
 * Operaciones administrativas del módulo Security alineadas estrictamente
 * con `backend/docs/API_SECURITY.md`.
 */
export const adminService = {
  async pingSecurity(): Promise<SecurityPingResponse> {
    const { data } = await useApi().get<SecurityPingResponse>('/security/ping')
    return data
  },

  async suspendUser(userId: string): Promise<void> {
    await useApi().post(
      `/users/${encodeURIComponent(userId.trim())}/suspend`,
      null,
      withIdempotency(newIdempotencyKey()),
    )
  },

  async activateUser(userId: string): Promise<void> {
    await useApi().post(
      `/users/${encodeURIComponent(userId.trim())}/activate`,
      null,
      withIdempotency(newIdempotencyKey()),
    )
  },

  async resetUserMfa(userId: string): Promise<void> {
    await useApi().post(
      `/users/${encodeURIComponent(userId.trim())}/mfa/reset`,
      null,
      withIdempotency(newIdempotencyKey()),
    )
  },

  async requestUserRoleChange(
    userId: string,
    payload: ChangeUserRoleRequest,
  ): Promise<ApprovalRequestDto> {
    const { data } = await useApi().put<ApprovalRequestDto>(
      `/users/${encodeURIComponent(userId.trim())}/role`,
      {
        newRole: payload.newRole,
        reason: payload.reason.trim(),
      },
      withIdempotency(newIdempotencyKey()),
    )
    return data
  },
}
