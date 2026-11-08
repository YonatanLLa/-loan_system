import type { AuditLogEntry, Page } from '@prestamos/api-client'
import { useApi } from '@/app/api'

export interface AuditQueryFilters {
  entity?: string
  action?: string
  user?: string
  from?: string
  to?: string
  page: number
  pageSize: number
}

export const auditService = {
  async getEntities(): Promise<string[]> {
    const { data } = await useApi().get<string[]>('/audit/entities')
    return data
  },

  async getActions(): Promise<string[]> {
    const { data } = await useApi().get<string[]>('/audit/actions')
    return data
  },

  async getLogs(filters: AuditQueryFilters): Promise<Page<AuditLogEntry>> {
    const params: Record<string, string | number> = {
      page: filters.page,
      pageSize: filters.pageSize,
    }
    if (filters.entity) params.entity = filters.entity
    if (filters.action) params.action = filters.action
    if (filters.user?.trim()) params.user = filters.user.trim()
    if (filters.from) params.from = filters.from
    if (filters.to) params.to = filters.to

    const { data } = await useApi().get<Page<AuditLogEntry>>('/audit-logs', { params })
    return data
  },
}
