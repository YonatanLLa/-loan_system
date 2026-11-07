import { ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import type { AuditLogEntry, Page } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { auditService } from '../services/audit.service'

export function useAuditLog() {
  const loading = ref(false)
  const auditPage = ref<Page<AuditLogEntry>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: 15,
  })

  const availableEntities = ref<string[]>([])
  const availableActions = ref<string[]>([])

  const filters = ref({
    entity: '',
    action: '',
    user: '',
    from: '',
    to: '',
    page: 1,
  })

  const totalPages = ref(1)
  watch(
    () => auditPage.value.total,
    (total) => {
      totalPages.value = Math.max(1, Math.ceil(total / auditPage.value.pageSize))
    },
    { immediate: true },
  )

  async function loadCatalogs() {
    try {
      const [entities, actions] = await Promise.all([
        auditService.getEntities(),
        auditService.getActions(),
      ])
      availableEntities.value = entities
      availableActions.value = actions
    } catch {
      // Silencioso en caso de que el catálogo falle
    }
  }

  async function loadAuditLogs() {
    loading.value = true
    try {
      auditPage.value = await auditService.getLogs({
        ...filters.value,
        pageSize: auditPage.value.pageSize,
      })
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
    filters.value = {
      entity: '',
      action: '',
      user: '',
      from: '',
      to: '',
      page: 1,
    }
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
    availableEntities,
    availableActions,
    filters,
    totalPages,
    loadCatalogs,
    loadAuditLogs,
    onSearch,
    resetFilters,
    goToPage,
  }
}
