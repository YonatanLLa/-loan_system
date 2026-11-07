import { computed, ref } from 'vue'
import type { Page, ReceivableItem } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { collectionsService, type ReceivableQueryParams } from '../services/collections.service'

export function useReceivables(initialPageSize = 10) {
  const pageData = ref<Page<ReceivableItem>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: initialPageSize,
  })
  const loading = ref(false)
  const error = ref<string | null>(null)

  const type = ref<'' | 'VENCIDA' | 'PENDIENTE'>('')
  const query = ref('')
  const currentPage = ref(1)

  const items = computed(() => pageData.value.items)
  const total = computed(() => pageData.value.total)
  const totalPages = computed(() =>
    Math.max(1, Math.ceil(pageData.value.total / pageData.value.pageSize)),
  )

  async function loadReceivables() {
    loading.value = true
    error.value = null
    try {
      const params: ReceivableQueryParams = {
        page: currentPage.value,
        pageSize: pageData.value.pageSize,
        type: type.value,
        q: query.value,
      }
      pageData.value = await collectionsService.fetchReceivables(params)
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
      toast.error('Error al cargar cuentas por cobrar', { description: apiErr.detail })
    } finally {
      loading.value = false
    }
  }

  function applyFilters() {
    currentPage.value = 1
    void loadReceivables()
  }

  function setPage(nextPage: number) {
    if (nextPage < 1 || nextPage > totalPages.value) return
    currentPage.value = nextPage
    void loadReceivables()
  }

  function refresh() {
    return loadReceivables()
  }

  return {
    pageData,
    items,
    total,
    totalPages,
    currentPage,
    loading,
    error,
    type,
    query,
    loadReceivables,
    applyFilters,
    setPage,
    refresh,
  }
}
