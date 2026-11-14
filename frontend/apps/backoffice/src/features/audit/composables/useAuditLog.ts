import { ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import { z } from 'zod'

import { type AuditLogEntry, type AuditLogQuery, type Page, toApiError } from '@prestamos/api-client'

import { auditService } from '../services/audit.service'

export const AUDIT_ENTITY_SUGGESTIONS = [
  'users',
  'approval_requests',
  'refresh_tokens',
  'loans',
  'payments',
  'loan_products',
  'customers',
] as const

export const AUDIT_ACTION_OPTIONS = ['INSERT', 'UPDATE', 'DELETE'] as const

const uuidSchema = z.string().uuid()

export interface AuditFilterState {
  entity: string
  entityId: string
  action: string
  user: string
  from: string
  to: string
  approvalRequestId: string
  page: number
}

function createEmptyFilters(): AuditFilterState {
  return {
    entity: '',
    entityId: '',
    action: '',
    user: '',
    from: '',
    to: '',
    approvalRequestId: '',
    page: 1,
  }
}

export function useAuditLog(initialFilters: Partial<AuditFilterState> = {}) {
  const loading = ref(false)
  const auditPage = ref<Page<AuditLogEntry>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 20,
  })

  const filters = ref<AuditFilterState>({
    ...createEmptyFilters(),
    ...initialFilters,
  })
  const approvalRequestIdError = ref<string | null>(null)

  const totalPages = ref(1)
  watch(
    () => auditPage.value.total,
    (total) => {
      totalPages.value = Math.max(1, Math.ceil(total / auditPage.value.pageSize))
    },
    { immediate: true },
  )

  function buildQuery(): AuditLogQuery | null {
    const trimmedApprovalId = filters.value.approvalRequestId.trim()
    if (trimmedApprovalId && !uuidSchema.safeParse(trimmedApprovalId).success) {
      approvalRequestIdError.value = 'El ID de aprobación debe ser un UUID válido.'
      return null
    }
    approvalRequestIdError.value = null

    const trimmedUser = filters.value.user.trim()
    const userIsUuid = trimmedUser !== '' && uuidSchema.safeParse(trimmedUser).success

    return {
      entity: filters.value.entity.trim() || undefined,
      entityId: filters.value.entityId.trim() || undefined,
      action: filters.value.action.trim() || undefined,
      userId: userIsUuid ? trimmedUser : undefined,
      user: trimmedUser && !userIsUuid ? trimmedUser : undefined,
      from: filters.value.from || undefined,
      to: filters.value.to || undefined,
      approvalRequestId: trimmedApprovalId || undefined,
      page: filters.value.page,
      pageSize: auditPage.value.pageSize,
    }
  }

  async function loadAuditLogs() {
    const query = buildQuery()
    if (!query) return

    loading.value = true
    try {
      auditPage.value = await auditService.getLogs(query)
    } catch (error) {
      const apiError = toApiError(error)
      toast.error('Error al cargar bitácora', { description: apiError.detail })
    } finally {
      loading.value = false
    }
  }

  function onSearch() {
    filters.value.page = 1
    void loadAuditLogs()
  }

  function resetFilters() {
    filters.value = createEmptyFilters()
    approvalRequestIdError.value = null
    void loadAuditLogs()
  }

  function goToPage(p: number) {
    if (p < 1 || p > totalPages.value) return
    filters.value.page = p
    void loadAuditLogs()
  }

  return {
    loading,
    auditPage,
    filters,
    approvalRequestIdError,
    totalPages,
    loadAuditLogs,
    onSearch,
    resetFilters,
    goToPage,
  }
}
