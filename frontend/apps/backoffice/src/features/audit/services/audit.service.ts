import type { AuditLogEntry, AuditLogQuery, Page } from '@prestamos/api-client'

import { useApi } from '@/app/api'

export type AuditQueryFilters = AuditLogQuery

/**
 * Cliente HTTP de auditoría conectado únicamente a `GET /api/audit-logs`
 * (`backend/docs/API_SECURITY.md`).
 */
export const auditService = {
  async getLogs(filters: AuditQueryFilters): Promise<Page<AuditLogEntry>> {
    const params: Record<string, string | number> = {
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    }

    if (filters.entity?.trim()) params.entity = filters.entity.trim()
    if (filters.entityName?.trim()) params.entityName = filters.entityName.trim()
    if (filters.entityId?.trim()) params.entityId = filters.entityId.trim()
    if (filters.action?.trim()) params.action = filters.action.trim()
    if (filters.userId?.trim()) params.userId = filters.userId.trim()
    if (filters.user?.trim()) params.user = filters.user.trim()
    if (filters.from) params.from = filters.from
    if (filters.to) params.to = filters.to
    if (filters.approvalRequestId?.trim()) {
      params.approvalRequestId = filters.approvalRequestId.trim()
    }

    const { data } = await useApi().get<Page<AuditLogEntry>>('/audit-logs', { params })
    return data
  },
}
