import { computed, ref } from 'vue'
import type { AgingBucket, AgingRow, Page } from '@prestamos/api-client'
import { toApiError } from '@prestamos/api-client'
import { toast } from 'vue-sonner'

import { collectionsService, type AgingQueryParams } from '../services/collections.service'

export function useAging(initialPageSize = 10) {
  const pageData = ref<Page<AgingRow>>({
    items: [],
    total: 0,
    page: 1,
    pageSize: initialPageSize,
  })
  const bucketCounts = ref<Record<string, number>>({})
  const loading = ref(false)
  const error = ref<string | null>(null)

  // Filtros reactivos
  const bucket = ref<AgingBucket | ''>('')
  const assignedTo = ref('')
  const productName = ref('')
  const minAmount = ref('')
  const maxAmount = ref('')
  const query = ref('')
  const currentPage = ref(1)

  const items = computed(() => pageData.value.items)
  const total = computed(() => pageData.value.total)
  const totalPages = computed(() =>
    Math.max(1, Math.ceil(pageData.value.total / pageData.value.pageSize)),
  )

  async function loadAging() {
    loading.value = true
    error.value = null
    try {
      const params: AgingQueryParams = {
        page: currentPage.value,
        pageSize: pageData.value.pageSize,
        bucket: bucket.value,
        assignedTo: assignedTo.value,
        productName: productName.value,
        minAmount: minAmount.value,
        maxAmount: maxAmount.value,
        q: query.value,
      }
      pageData.value = await collectionsService.fetchAging(params)
    } catch (err) {
      const apiErr = toApiError(err)
      error.value = apiErr.detail
      toast.error('Error al cargar la cartera de aging', { description: apiErr.detail })
    } finally {
      loading.value = false
    }
  }

  async function loadBucketCounts() {
    try {
      bucketCounts.value = await collectionsService.fetchAgingBucketCounts()
    } catch {
      bucketCounts.value = {}
    }
  }

  function applyFilters() {
    currentPage.value = 1
    void loadAging()
  }

  function chooseBucket(next: AgingBucket | '') {
    bucket.value = next
    applyFilters()
  }

  function setPage(nextPage: number) {
    if (nextPage < 1 || nextPage > totalPages.value) return
    currentPage.value = nextPage
    void loadAging()
  }

  async function refresh() {
    await Promise.all([loadAging(), loadBucketCounts()])
  }

  return {
    pageData,
    items,
    total,
    totalPages,
    currentPage,
    bucketCounts,
    loading,
    error,
    bucket,
    assignedTo,
    productName,
    minAmount,
    maxAmount,
    query,
    loadAging,
    loadBucketCounts,
    applyFilters,
    chooseBucket,
    setPage,
    refresh,
  }
}
