import {
  type ApprovalDecisionResponse,
  type ApprovalQuery,
  type ApprovalRequestDto,
  type ApprovalRequestSummary,
  type ApproveApprovalRequest,
  type Page,
  type RejectApprovalRequest,
  newIdempotencyKey,
  withIdempotency,
} from '@prestamos/api-client'

import { useApi } from '@/app/api'

/**
 * Cliente HTTP de aprobaciones de cuatro ojos alineado con `API_SECURITY.md`.
 */
export const approvalsService = {
  async getApprovals(query: ApprovalQuery = {}): Promise<Page<ApprovalRequestSummary>> {
    const params: Record<string, string | number> = {
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 20,
    }
    if (query.scope) params.scope = query.scope
    if (query.status) params.status = query.status
    if (query.operationType) params.operationType = query.operationType
    if (query.targetId?.trim()) params.targetId = query.targetId.trim()
    if (query.requestedBy?.trim()) params.requestedBy = query.requestedBy.trim()

    const { data } = await useApi().get<Page<ApprovalRequestSummary>>('/approvals', { params })
    return data
  },

  async getApprovalById(id: string): Promise<ApprovalRequestDto> {
    const { data } = await useApi().get<ApprovalRequestDto>(
      `/approvals/${encodeURIComponent(id.trim())}`,
    )
    return data
  },

  async approveApproval(
    id: string,
    payload: ApproveApprovalRequest,
  ): Promise<ApprovalDecisionResponse> {
    const { data } = await useApi().post<ApprovalDecisionResponse>(
      `/approvals/${encodeURIComponent(id.trim())}/approve`,
      {
        comment: payload.comment?.trim() || undefined,
        mfaCode: payload.mfaCode?.trim() || undefined,
      },
      withIdempotency(newIdempotencyKey()),
    )
    return data
  },

  async rejectApproval(
    id: string,
    payload: RejectApprovalRequest,
  ): Promise<ApprovalDecisionResponse> {
    const { data } = await useApi().post<ApprovalDecisionResponse>(
      `/approvals/${encodeURIComponent(id.trim())}/reject`,
      { comment: payload.comment.trim() },
      withIdempotency(newIdempotencyKey()),
    )
    return data
  },

  async cancelApproval(id: string): Promise<ApprovalDecisionResponse> {
    const { data } = await useApi().post<ApprovalDecisionResponse>(
      `/approvals/${encodeURIComponent(id.trim())}/cancel`,
      null,
      withIdempotency(newIdempotencyKey()),
    )
    return data
  },
}
